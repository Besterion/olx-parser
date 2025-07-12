using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // ▸ краще брати з Environment, але залишаю, як було
        string botToken = "7954672616:AAF4LoS3NEipXmWeTs-9pr9OX_n6SrrRFUE";
        string chatId = "5991091165";

        var categories = new Dictionary<string, List<string>>
        {
            ["Кросівки"] = new()
            {
                "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&search%5Border%5D=created_at:desc",
                "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&page=2&search%5Border%5D=created_at:desc",
                "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&page=3&search%5Border%5D=created_at:desc"
            },
            ["Футбол"] = new()
            {
                "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&search%5Border%5D=created_at:desc",
                "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&page=2&search%5Border%5D=created_at:desc",
                "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&page=3&search%5Border%5D=created_at:desc"
            }
        };

        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

        foreach (var (cat, urls) in categories)
        {
            string fileName = $"{cat}.txt";
            var processed = File.Exists(fileName)
                ? new HashSet<string>(await File.ReadAllLinesAsync(fileName))
                : new HashSet<string>();

            var fresh = new List<string>();

            foreach (var url in urls)
            {
                Console.WriteLine($"\n===== {cat} =====\n{url}\n");

                string html = await http.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var cards = doc.DocumentNode.SelectNodes("//div[@data-cy='l-card']");
                if (cards == null)
                {
                    Console.WriteLine("❌ Карток не знайдено.");
                    continue;
                }

                foreach (var card in cards)
                {
                    // дата + місто
                    var dateNode = card.SelectSingleNode(".//p[@data-testid='location-date']");
                    string dateText = dateNode?.InnerText?.Trim() ?? "";

                    if (!dateText.Contains("Сьогодні", StringComparison.OrdinalIgnoreCase))
                        continue;                           // не сьогодні → пропускаємо

                    // посилання
                    var linkNode = card.SelectSingleNode(".//a[@href]");
                    string link = linkNode?.GetAttributeValue("href", "") ?? "";
                    if (string.IsNullOrWhiteSpace(link)) continue;
                    if (!link.StartsWith("http")) link = "https://www.olx.ua" + link;

                    if (processed.Add(link))              // Add==true → нового ще не було
                        fresh.Add(link);
                }
            }

            if (fresh.Count == 0)
            {
                Console.WriteLine($"ℹ️ У «{cat}» нових оголошень немає.");
                continue;
            }

            await File.AppendAllLinesAsync(fileName, fresh);

            foreach (var link in fresh)
            {
                Console.WriteLine($"🆕 {link}");
                await SendTelegram(botToken, chatId, link);
            }
        }
    }

    static async Task SendTelegram(string token, string chatId, string text)
    {
        using var cli = new HttpClient();
        string api = $"https://api.telegram.org/bot{token}/sendMessage" +
                     $"?chat_id={chatId}&text={Uri.EscapeDataString(text)}";
        await cli.GetAsync(api);
    }
}
