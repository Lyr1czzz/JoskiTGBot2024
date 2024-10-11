using System;
using Microsoft.Extensions.Configuration;

namespace JoskiTGBot2024
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Загружаем конфигурацию из appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // Устанавливаем базовый путь
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // Добавляем файл конфигурации
                .Build();

            // Получаем токен из конфигурационного файла
            var botToken = config["BotToken"];

            // Запускаем бот с токеном
            var botService = new BotService(botToken);
            botService.Start();

            Console.WriteLine("Бот запущен. Нажмите Enter для завершения.");
            Console.ReadLine();
        }
    }
}
