using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using JoskiTGBot2024.Database;
using JoskiTGBot2024.Models;
using JoskiTGBot2024.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;
using UserModel = JoskiTGBot2024.Models.User;
using System.Text.RegularExpressions;

namespace JoskiTGBot2024
{
    public class BotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly ExcelService _excelService;
        bool fileRole;

        public BotService(string token)
        {
            _botClient = new TelegramBotClient(token);
            _dbContext = new ApplicationDbContext();
            _excelService = new ExcelService();
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
                var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);
                var command = message.Text;

                // Проверка ввода данных для группы или ФИО после смены роли
                if (user != null && !string.IsNullOrEmpty(user.Role) && string.IsNullOrEmpty(user.GroupName))
                {
                    if (user.Role == "Учащийся" && Regex.IsMatch(command, @"^[А-ЯЁ]{1,2}-\d{4}$"))
                    {
                        await ChangeUserGroupOrFIO(user.TelegramUserId, command);
                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"Группа успешно сохранена как {command}.");
                    }
                    else if (user.Role == "Преподаватель" && Regex.IsMatch(command, @"^[А-ЯЁ][а-яё]+ [А-ЯЁ]\.[А-ЯЁ]\.$"))
                    {
                        await ChangeUserGroupOrFIO(user.TelegramUserId, command);
                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"ФИО успешно сохранено как {command}.");
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, введите корректные данные.");
                    }
                    return;
                }

                switch (command)
                {
                    case "/start":
                        if (user != null)
                        {
                            _dbContext.Users.Remove(user);
                            await _dbContext.SaveChangesAsync();
                        }

                        await RegisterEmptyUser(message.Chat.Id);
                        if (IsAdmin(message.From.Id))
                        {
                            await ShowAdminMenu(message.Chat.Id);
                        }
                        else
                        {
                            await ShowUserMenu(message.Chat.Id);
                        }
                        break;

                    default:
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Неизвестная команда. Пожалуйста, выберите действие из меню.");
                        break;
                }
            }

            if (update.CallbackQuery != null)
            {
                var callbackQuery = update.CallbackQuery;
                var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.Message.Chat.Id);

                switch (callbackQuery.Data)
                {
                    case "change_role":
                        await AskForRole(callbackQuery.Message.Chat.Id);
                        break;

                    case "role_student":
                        await ChangeUserRole(callbackQuery.Message.Chat.Id, "Учащийся");
                        await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Роль 'Учащийся' установлена. Пожалуйста, введите вашу группу.");
                        break;

                    case "role_teacher":
                        await ChangeUserRole(callbackQuery.Message.Chat.Id, "Преподаватель");
                        await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Роль 'Преподаватель' установлена. Пожалуйста, введите ваше ФИО.");
                        break;

                    case "upload_schedule_students":
                        await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Пожалуйста, отправьте файл Excel с расписанием для студентов.");
                        fileRole = false;
                        break;

                    case "upload_schedule_teachers":
                        await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Пожалуйста, отправьте файл Excel с расписанием для преподавателей.");
                        fileRole = true;
                        break;

                    default:
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Вы написали какую-то бяку, попробуйте снова.");
                        break;
                }
            }
        }

        // Метод для отображения меню для обычного пользователя
        private async Task ShowUserMenu(long chatId)
        {
            var userMenu = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Сменить роль", "change_role")
                }
            });

            await _botClient.SendTextMessageAsync(chatId, "Меню действий:", replyMarkup: userMenu);
        }

        // Метод для отображения меню для администратора
        private async Task ShowAdminMenu(long chatId)
        {
            var adminMenu = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📚 Отправить расписание для студентов", "upload_schedule_students"),
                    InlineKeyboardButton.WithCallbackData("👨‍🏫 Отправить расписание для преподавателей", "upload_schedule_teachers")
                }
            });

            await _botClient.SendTextMessageAsync(chatId, "Меню администратора:", replyMarkup: adminMenu);
        }

        // Метод для запроса изменения группы или ФИО
        private async Task ChangeUserGroupOrFIO(long chatId, string newValue)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user != null)
            {
                user.GroupName = newValue;
                await _dbContext.SaveChangesAsync();
            }
        }

        // Метод для запроса выбора роли
        private async Task AskForRole(long chatId)
        {
            var roleButtons = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Учащийся", "role_student"),
                InlineKeyboardButton.WithCallbackData("Преподаватель", "role_teacher")
            });

            await _botClient.SendTextMessageAsync(chatId, "Пожалуйста, выберите вашу роль:", replyMarkup: roleButtons);
        }

        // Метод для смены роли пользователя
        private async Task ChangeUserRole(long chatId, string role)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user != null)
            {
                user.Role = role;
                user.GroupName = ""; // Сбрасываем группу/ФИО при смене роли
                await _dbContext.SaveChangesAsync();
            }
        }

        // Метод для регистрации нового пользователя с пустыми полями, кроме ChatId
        private async Task RegisterEmptyUser(long chatId)
        {
            var newUser = new UserModel
            {
                TelegramUserId = chatId,
                Role = "", // роль еще не выбрана
                GroupName = "" // пустая группа/ФИО
            };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();
        }

        private bool IsAdmin(long userId)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == userId);
            return user != null && user.IsAdmin;
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
