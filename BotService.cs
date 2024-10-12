using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using JoskiTGBot2024.Database;
using JoskiTGBot2024.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Telegram.Bot.Types.ReplyMarkups;

using UserModel = JoskiTGBot2024.Models.User;

namespace JoskiTGBot2024
{
    public class BotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ApplicationDbContext _dbContext;

        public BotService(string token)
        {
            _botClient = new TelegramBotClient(token);
            _dbContext = new ApplicationDbContext();
        }

        public void Start()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // Получаем все типы обновлений
            };

            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions);
            Console.WriteLine("Бот запущен. Нажмите Enter для завершения.");
            Console.ReadLine();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message != null)
            {
                var message = update.Message;

                if (message.Text != null && message.Text.StartsWith("/start"))
                {
                    // Показываем выбор роли через кнопки
                    var roleKeyboard = new ReplyKeyboardMarkup(new[]
                    {
                        new KeyboardButton("Учащийся"),
                        new KeyboardButton("Преподаватель")
                    })
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    };

                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Выберите вашу роль:", replyMarkup: roleKeyboard);
                }
                else if (message.Text == "Учащийся" || message.Text == "Преподаватель")
                {
                    // Сохраняем выбранную роль в БД
                    var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);
                    if (user == null)
                    {
                        user = new UserModel
                        {
                            TelegramUserId = message.Chat.Id,
                            Role = message.Text // Сохраняем роль
                        };
                        _dbContext.Users.Add(user);
                    }
                    else
                    {
                        user.Role = message.Text; // Обновляем роль
                    }

                    await _dbContext.SaveChangesAsync();

                    // Если учащийся, показываем кнопку для ввода группы
                    if (message.Text == "Учащийся")
                    {
                        var groupKeyboard = new ReplyKeyboardMarkup(new[]
                        {
                            new KeyboardButton("Ввести группу")
                        })
                        {
                            ResizeKeyboard = true,
                            OneTimeKeyboard = false
                        };

                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Нажмите кнопку, чтобы ввести вашу группу:", replyMarkup: groupKeyboard);
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы выбрали роль преподавателя.");
                    }
                }
                else if (message.Text == "Ввести группу")
                {
                    // Показываем запрос на ввод группы
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Введите название вашей группы:", replyMarkup: new ReplyKeyboardRemove());
                }
                else
                {
                    // Сохраняем группу в БД
                    var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);
                    if (user != null && user.Role == "Учащийся")
                    {
                        user.GroupName = message.Text;
                        await _dbContext.SaveChangesAsync();
                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"Ваша группа была успешно сохранена: {message.Text}");
                    }
                }
            }
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
