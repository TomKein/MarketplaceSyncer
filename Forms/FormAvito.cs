using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Selen.Base;
using Selen.Tools;

namespace Selen.Forms {
    public partial class FormAvito : Form {

        readonly string _catsFile = @"..\data\avito\avito_categories.xml";
        XDocument _cats;
        IEnumerable<XElement> _rules;
        TreeNode _treeParentNodeSelected;
        int _treeRuleIndexSelected;
        int _priceLevel;
        bool _progress = false;

        public FormAvito() {
            InitializeComponent();
            _priceLevel = DB.GetParamInt("avito.priceLevel");
        }
        private void FormAvito_Load(object sender, EventArgs e) {
            FillXmlTree();
            //treeView1.SelectedNode = treeView1.Nodes[0];
            //ListBoxesUpdate();
        }
        private void FillXmlTree() {
            try {
                _cats = XDocument.Load(_catsFile);
                _rules = _cats.Descendants("Rule");
                XElement dir = _cats.Root;
                TreeNode node = new TreeNode((string) dir.Attribute("Name"));
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(node);
                GetTree(dir, node);
                treeView1.ExpandAll();
            } catch (Exception x) {
                Log.Add(x.Message);
                MessageBox.Show(x.Message);
            }

        }
        void GetTree(XElement dir, TreeNode node) {
            foreach (XElement child in dir.Elements()) {
                var name = child.Attribute("Name")?.Value;
                if (name == null) {
                    name = child.Name.ToString();
                    if (name == "Rule") {
                        name = GetValue(child);
                        var childNode = new TreeNode(name);
                        node.Nodes.Add(childNode);
                    }
                } else {
                    var childNode = new TreeNode(name);
                    node.Nodes.Add(childNode);
                    GetTree(child, childNode);
                }
            }
        }
        string GetValue(XElement child) {
            var s = new StringBuilder();
            foreach (XElement node in child.Elements()) {
                if (s.Length > 0)
                    s.Append(", ");
                else
                    s.Append("=> ");
                if (node.Name == "Starts")
                    s.Append("!");
                else if (node.Name == "Contains")
                    s.Append("*");
                else if (node.Name == "MaxWeight")
                    s.Append("w<");
                else if (node.Name == "MinWeight")
                    s.Append("w>");
                else if (node.Name == "MaxLength")
                    s.Append("l<");
                else if (node.Name == "MinLength")
                    s.Append("l>");
                s.Append(node.Value);
            }
            return s.ToString();
        }
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e) {
            if (treeView1.SelectedNode == null || e.Action != TreeViewAction.ByMouse)
                return;
            if (treeView1.SelectedNode.Text.StartsWith("=>")) {
                //порядковый номер в категории для выбранногого правила в контроле
                _treeRuleIndexSelected = treeView1.SelectedNode.Index;
                //родительская категория для правила в контроле
                _treeParentNodeSelected = treeView1.SelectedNode.Parent;
                //находим категорию в xml, соответствующую выбранной в контроле
                var parentNode = _rules.Where(s => s.Parent.Attribute("Name").Value == _treeParentNodeSelected.Text);
                //выбираем нужное правило по порядковому номеру
                var selectedRule = parentNode.ToList()[_treeRuleIndexSelected];
                //элементы правила выбираем в массив
                var ruleValues = selectedRule.Elements().ToArray();
                dataGridView1.Rows.Clear();
                for (int i = 0; i < ruleValues.Length; i++) {
                    dataGridView1.Rows.Add();
                    //обращаемся к первому столбку как к комбобокс типу
                    DataGridViewComboBoxCell cell = dataGridView1[0, i] as DataGridViewComboBoxCell;
                    cell.Value = ruleValues[i].Name.ToString();
                    dataGridView1[1, i].Value = ruleValues[i].Value.ToString();
                }
                //dataGridView1.ReadOnly = false;
            } else {
                dataGridView1.Rows.Clear();
                _treeParentNodeSelected = treeView1.SelectedNode;
                //dataGridView1.ReadOnly = true;
            }
            ListBoxesUpdate();
        }
        private async void buttonSave_Click(object sender, EventArgs e) {
            if (treeView1.SelectedNode == null)
                return;
            if (dataGridView1.Rows.Count == 1)
                return;
            if (treeView1.SelectedNode.Text.StartsWith("=>")) {
                //порядковый номер правила в выбранной категории
                var ind = treeView1.SelectedNode.Index;
                //категория правила
                var parSelected = treeView1.SelectedNode.Parent;
                //находим категорию в xml, соответствующую выбранной в контроле
                var parentNode = _rules.Where(s => s.Parent.Attribute("Name").Value == parSelected.Text);
                //выбираем нужное правило по порядковому номеру
                var selectedRule = parentNode.ToList()[ind];
                //убираем из выбранного правила все условия
                var rule = selectedRule.Nodes().ToList();
                for (int i = rule.Count - 1; i >= 0; i--) {
                    rule[i].Remove();
                }
                //новые условия правила
                for (int i = 0; i < dataGridView1.Rows.Count - 1; i++) {
                    //добавляем новые условия в правило
                    var name = dataGridView1[0, i].Value?.ToString();
                    if (string.IsNullOrEmpty(name))
                        continue;
                    var value = dataGridView1[1, i].Value?.ToString();
                    if (string.IsNullOrEmpty(value))
                        continue;
                    XElement el = new XElement(name);
                    el.Value = value;
                    selectedRule.Add(el);
                }
                _cats.Save(_catsFile);
                FillXmlTree();
                treeView1.Select();
                SelectNode(_treeParentNodeSelected.Text, treeView1.Nodes);
                ListBoxesUpdate();
            }
        }
        void SelectNode(string text, TreeNodeCollection nodes) {
            foreach (TreeNode node in nodes) {
                if (node.Text == text) {
                    treeView1.SelectedNode = node;
                    return;
                }
                SelectNode(text, node.Nodes);
            }
        }
        async void ListBoxesUpdate() {
            while (Class365API.Status == SyncStatus.NeedUpdate)
                await Task.Delay(30000);

            if (_treeParentNodeSelected==null) return;
            //список товаров для авито
            var goodList = Class365API._bus.Where(w => w.Price >= _priceLevel && w.images.Count > 0 &&
                                            (w.Amount > 0 ||
                                             DateTime.Parse(w.updated).AddHours(2).AddDays(2) > Class365API.LastScanTime ||
                                             DateTime.Parse(w.updated_remains_prices).AddHours(2).AddDays(2) > Class365API.LastScanTime))
                                 .OrderByDescending(o => o.Price).ToList();
            //завершаю работающий процесс
            if (_progress) _progress = false;
            while (progressBar1.Visible) {
                await Task.Delay(100);
            }
            _progress = true;
            //заполняю листбоксы
            progressBar1.Value = 0;
            progressBar1.Visible=true;
            listBoxExceptions.Items.Clear();
            listBoxCategory.Items.Clear();
            await Task.Factory.StartNew(() =>{
                for (int i = 0; i < goodList.Count; i++) {
                    if (!_progress)
                        return;
                    try {
                        var cat = GetCategoryAvito(goodList[i]);
                        if (!cat.Any())
                            listBoxExceptions.Invoke(new Action(() => {
                                listBoxExceptions.Items?.Add(goodList[i].name);
                            }));
                        else if (cat.Any(c => c.Value == _treeParentNodeSelected.Text))
                            listBoxCategory.Invoke(new Action(() => {
                                listBoxCategory.Items.Add(goodList[i].name);
                            }));
                        if (i % 50 == 0 || i==goodList.Count-1) {
                            progressBar1.Invoke(new Action(() => {
                                progressBar1.Value = (int) ((float) (i * 100) / goodList.Count);
                            }));
                            labelCategory.Invoke(new Action(() => {
                                labelCategory.Text = "Попадает в категорию: " + listBoxCategory.Items.Count;
                            }));
                            labelExceptions.Invoke(new Action(() => {
                                labelExceptions.Text = "Не попадает в категории: " + listBoxExceptions.Items.Count;
                            }));
                        }
                    } catch{}
                }
            });
            progressBar1.Visible = false;
        }
        Dictionary<string, string> GetCategoryAvito(GoodObject b) {
            var name = b.name.ToLowerInvariant()
                             .Replace(@"б\у", "").Replace("б/у", "").Replace("б.у.", "").Replace("б.у", "").Trim();
            var d = new Dictionary<string, string>();

            foreach (var rule in _rules) {
                var conditions = rule.Elements();
                var eq = true;
                foreach (var condition in conditions) {
                    if (!eq)
                        break;
                    if (condition.Name == "Starts" && !name.StartsWith(condition.Value))
                        eq = false;
                    else if (condition.Name == "Contains" && !name.Contains(condition.Value))
                        eq = false;
                    else if (condition.Name == "MaxWeight" && b.Weight > float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                    else if (condition.Name == "MinWeight" && b.Weight < float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                    else if (condition.Name == "MaxLength" && b.GetLength() > float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                    else if (condition.Name == "MinLength" && b.GetLength() < float.Parse(condition.Value.Replace(".", ",")))
                        eq = false;
                }
                if (eq) {
                    GetParams(rule, d);
                    if (d.Any())
                        return d;
                }
            }
            return d;
        }
        void GetParams(XElement rule, Dictionary<string, string> d) {
            var parent = rule.Parent;
            if (parent != null) {
                GetParams(parent, d);
                d.Add(parent.Name.ToString(), parent.Attribute("Name").Value);
            }
        }
        private void listBoxExceptions_DoubleClick(object sender, EventArgs e) {
            //проверка выбора категории
            if (_treeParentNodeSelected==null)
                return;
            //получаем название товара
            var selectedString = listBoxExceptions.Items[listBoxExceptions.SelectedIndex];
            var splitedString = selectedString.ToString().Split(' ');
            //добавляем правило
            XElement ruleElement = new XElement("Rule");
            ruleElement.Add(new XElement("Starts") { Value = splitedString[0].ToLowerInvariant() });
            ruleElement.Add(new XElement("Contains") { Value = splitedString[1].ToLowerInvariant() });
            _cats.Descendants()
                 .First(f=>f.Attribute("Name")?.Value==_treeParentNodeSelected?.Text &&
                           f.Parent.Attribute("Name")?.Value==_treeParentNodeSelected.Parent?.Text)?
                 .Add(ruleElement);
            //сохраняю и обновляю дерево
            _cats.Save(_catsFile);
            FillXmlTree();
            //while (treeView1.Nodes[0].Nodes.Count == 0) {
            //await Task.Delay(2000); 
            //}
            treeView1.Select();
            SelectNode(_treeParentNodeSelected.Text, treeView1.Nodes);
        }

        private void listBoxExceptions_Click(object sender, EventArgs e) {
            var txt = listBoxExceptions.SelectedItem.ToString();
            Clipboard.SetText(txt); 
        }
    }
}