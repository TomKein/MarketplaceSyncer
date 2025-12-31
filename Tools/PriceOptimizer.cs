using System;
using System.Collections.Generic;
using System.Linq;

public class SaleData {
    public DateTime Date { get; set; }
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; } // свойство для учета закупочной цены
}

public class PriceOptimizer {
    public static decimal CalculateOptimalPrice(List<SaleData> sales, int periodInWeeks = 1) {
        if (sales == null || sales.Count < 2)
            throw new ArgumentException("Недостаточно данных для расчета.");

        // Определяем последнюю дату в данных
        DateTime maxDate = sales.Max(s => s.Date);

        // Группируем продажи по неделям от последней даты
        var groupedSales = sales
            .GroupBy(s => (maxDate - s.Date).Days / (7 * periodInWeeks))
            .Select(g => new {
                Price = g.Average(s => s.Price),
                CostPrice = g.Average(s => s.CostPrice), // Средняя себестоимость за период
                Quantity = g.Count()
            })
            .OrderBy(g => g.Price)
            .ToList();

        if (groupedSales.Count < 2)
            throw new ArgumentException("Недостаточно разных цен для вычисления коэффициентов.");

        // Регрессионный анализ для нахождения коэффициентов a и b
        int n = groupedSales.Count;
        double sumX = groupedSales.Sum(g => (double) g.Price);
        double sumY = groupedSales.Sum(g => g.Quantity);
        double sumXY = groupedSales.Sum(g => (double) g.Price * g.Quantity);
        double sumX2 = groupedSales.Sum(g => (double) g.Price * (double) g.Price);

        double b = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        double a = (sumY - b * sumX) / n;

        if (b <= 0)
            throw new InvalidOperationException("Не удалось найти корректную зависимость спроса от цены.");

        // Оптимальная цена с учетом индивидуальной себестоимости
        var optimalPrices = groupedSales.Select(g => (g.Quantity > 0) ? (a + b * (double) g.CostPrice) / (2 * b) : 0).Where(p => p > 0);

        decimal optimalPrice = (decimal) optimalPrices.Average();

        return Math.Round(optimalPrice, 2);
    }
}
