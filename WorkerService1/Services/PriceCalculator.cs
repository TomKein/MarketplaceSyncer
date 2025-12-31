namespace WorkerService1.Services;

/// <summary>
/// Сервис для расчета и округления цен
/// </summary>
public class PriceCalculator
{
    /// <summary>
    /// Вычислить новую цену с наценкой и округлением
    /// </summary>
    /// <param name="originalPrice">Исходная цена</param>
    /// <param name="markupPercent">Процент наценки (например, 15 для 15%)</param>
    /// <param name="roundingStep">Шаг округления (например, 50 для округления до 50 руб)</param>
    /// <returns>Новая цена с наценкой и округлением</returns>
    public static decimal CalculateNewPrice(decimal originalPrice, decimal markupPercent, decimal roundingStep)
    {
        // Шаг 1: Применить наценку
        var priceWithMarkup = originalPrice * (1 + markupPercent / 100m);

        // Шаг 2: Округлить до ближайшего значения, кратного roundingStep
        if (roundingStep <= 0)
        {
            return Math.Round(priceWithMarkup, 2);
        }

        var roundedPrice = Math.Round(priceWithMarkup / roundingStep) * roundingStep;

        return roundedPrice;
    }

    /// <summary>
    /// Проверить, нужно ли обновлять цену (сравнение с учетом погрешности)
    /// </summary>
    public static bool ShouldUpdatePrice(decimal currentPrice, decimal calculatedPrice, decimal tolerance)
    {
        return Math.Abs(currentPrice - calculatedPrice) > tolerance;
    }
}
