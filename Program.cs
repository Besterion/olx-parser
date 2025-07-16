using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        string botToken = "7954672616:AAF4LoS3NEipXmWeTs-9pr9OX_n6SrrRFUE";
        string chatId = "-1002710458430";

        var categories = new Dictionary<string, List<string>>
        {
            ["Кросівки"] = new()
            {
                "https://www.olx.ua/uk/moda-i-stil/muzhskaya-obuv/krossovki/zal-trenirovki/?currency=UAH&min_id=893747056&reason=observed_search&search%5Border%5D=created_at%3Adesc"
            },
            ["Футбол"] = new()
            {
                "https://www.olx.ua/uk/hobbi-otdyh-i-sport/sport-otdyh/futbol/?currency=UAH&min_id=893752454&reason=observed_search&search%5Border%5D=created_at%3Adesc"
            }
        };

        var handler = new HttpClientHandler();
        handler.CookieContainer = new CookieContainer();

        // Додаємо кукі з браузера
        handler.CookieContainer.Add(new Uri("https://www.olx.ua"), new Cookie("auth_state", "eyJzdWIiOiJlZGY2YTZjOC1hMzgzLTQ3ZGYtYmRhMC05ZTJlZjQwMDYyNjIifQ=="));

        var http = new HttpClient(handler);
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
                    // 1. залишаємо лише картки з міткою «Новинка»
                    var tagNode = card.SelectSingleNode(".//p[normalize-space()='Новинка']") ??
                                  card.SelectSingleNode(".//p[contains(text(),'Новинка')]");
                    if (tagNode == null) continue;

                    // 2. дістаємо посилання
                    var linkNode = card.SelectSingleNode(".//a[@href]");
                    string link = linkNode?.GetAttributeValue("href", "") ?? "";
                    if (string.IsNullOrWhiteSpace(link)) continue;
                    if (!link.StartsWith("http")) link = "https://www.olx.ua" + link;

                    // 3. фільтр на унікальність і додавання у список «свіжих»
                    if (processed.Add(link))
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
