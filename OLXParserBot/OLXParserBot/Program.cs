using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 🔑 Telegram дані
        string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        string chatId = Environment.GetEnvironmentVariable("CHAT_ID");

        Dictionary<string, List<string>> categories = new Dictionary<string, List<string>>
        {
            {
                "Кросівки", new List<string>
                {
                    "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&search%5Border%5D=created_at:desc",
                    "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&page=2&search%5Border%5D=created_at:desc",
                    "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&page=3&search%5Border%5D=created_at:desc"
                }
            },
            {
                "Футбол", new List<string>
                {
                    "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&search%5Border%5D=created_at:desc",
                    "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&page=2&search%5Border%5D=created_at:desc",
                    "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&page=3&search%5Border%5D=created_at:desc"
                }
            }
        };

        HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

        foreach (var category in categories)
        {
            string categoryName = category.Key;
            string fileName = $"{categoryName}.txt";

            // Читання збережених оголошень
            HashSet<string> existingAds = new HashSet<string>();
            if (File.Exists(fileName))
            {
                var lines = await File.ReadAllLinesAsync(fileName);
                foreach (var line in lines)
                    existingAds.Add(line);
            }

            List<string> newAds = new List<string>();

            foreach (string categoryUrl in category.Value)
            {
                Console.WriteLine($"\n=======================");
                Console.WriteLine($"🏷 Категорія: {categoryName}");
                Console.WriteLine($"🔗 {categoryUrl}");
                Console.WriteLine($"=======================\n");

                var html = await httpClient.GetStringAsync(categoryUrl);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                var ads = htmlDoc.DocumentNode.SelectNodes("//div[@data-cy='l-card']");
                if (ads == null)
                {
                    Console.WriteLine("❌ Нічого не знайдено на цій сторінці!\n");
                    continue;
                }

                foreach (var ad in ads)
                {
                    var dateNode = ad.SelectSingleNode(".//span[@data-cy='ad-posted-at']");
                    string dateText = dateNode?.InnerText?.Trim() ?? "";

                    if (!dateText.StartsWith("Сьогодні"))
                        continue;

                    var linkNode = ad.SelectSingleNode(".//a[@href]");
                    string link = linkNode?.GetAttributeValue("href", "");

                    if (string.IsNullOrWhiteSpace(link))
                        continue;

                    if (!link.StartsWith("http"))
                        link = "https://www.olx.ua" + link;

                    var titleNode = ad.SelectSingleNode(".//h4");
                    string title = titleNode?.InnerText?.Trim() ?? "[без назви]";

                    string adLine = $"{title} | {link}";

                    if (!existingAds.Contains(adLine))
                        newAds.Add(adLine);
                }
            }

            if (newAds.Count == 0)
            {
                Console.WriteLine($"ℹ️ В категорії '{categoryName}' нових оголошень немає.");
            }
            else
            {
                await File.AppendAllLinesAsync(fileName, newAds);

                foreach (var adLine in newAds)
                {
                    Console.WriteLine($"🆕 {adLine}");

                    // Відправити в Telegram
                    await SendTelegramMessage(botToken, chatId, adLine);
                }
            }
        }
    }

    // 🔔 Метод для надсилання повідомлень у Telegram
    static async Task SendTelegramMessage(string botToken, string chatId, string message)
    {
        using var httpClient = new HttpClient();
        string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";
        await httpClient.GetAsync(url);
    }
}
