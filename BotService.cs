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
// Псевдоним для устранения конфликта имен с Telegram.Bot.Types.User
using UserModel = JoskiTGBot2024.Models.User;
using Telegram.Bot.Types.ReplyMarkups;

namespace JoskiTGBot2024
{
    public class BotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly ExcelService _excelService;

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
                var command = message.Text?.Split(' ')[0];

                if (message.Document != null)
                {
                    if (IsAdmin(message.From.Id))
                    {
                        await ProcessAdminFile(message.Chat.Id, message.Document.FileId);
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Расписание было принято на обработку.");
                        return;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "У вас нет прав для загрузки файлов.");
                        return;
                    }
                }

                switch (command)
                {
                    case "/start":
                        {
                            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);

                            if (user == null || string.IsNullOrEmpty(user.GroupName))
                            {
                                await _botClient.SendTextMessageAsync(message.Chat.Id, "Для выбора группы напишите /group <название>");
                            }
                            else
                            {
                                await _botClient.SendTextMessageAsync(message.Chat.Id, $"Вы уже выбрали группу: {user.GroupName}. Для смены /group <название>");
                            }
                            break;
                        }

                    case "/group":
                        {
                            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);
                            string newGroupName = message.Text.Split(' ').Length > 1 ? message.Text.Split(' ')[1] : null;


                            if (user == null || string.IsNullOrEmpty(user.GroupName))
                            {
                                if (newGroupName != null)
                                {
                                    await RegisterUser(message.Chat.Id, newGroupName);
                                }
                                else
                                {
                                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Укажите новую группу после команды /group <название>");
                                }
                            }
                            else
                            {
                                if (newGroupName != null)
                                {
                                    await ChangeUserGroup(message.Chat.Id, newGroupName);
                                }
                                else
                                {
                                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Укажите новую группу после команды /group <название>");
                                }
                            }
                            break;
                        }

                    default:
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Я не понял что ты хотел. Попробуй снова");
                            break;
                        }
                }
            }
        }

        private async Task ChangeUserGroup(long chatId, string newGroupName)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user != null)
            {
                user.GroupName = newGroupName;
                await _dbContext.SaveChangesAsync();
                await _botClient.SendTextMessageAsync(chatId, $"Ваша группа была успешно изменена на {newGroupName}");
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "Вы не зарегистрированы. Пожалуйста, используйте команду /start для регистрации.");
            }
        }

        private async Task RegisterUser(long chatId, string groupName)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user == null)
            {
                var newUser = new UserModel { TelegramUserId = chatId, GroupName = groupName, IsAdmin = false };
                _dbContext.Users.Add(newUser);
                await _dbContext.SaveChangesAsync();
            }
            Console.WriteLine("новый чел " + chatId);
            foreach (var item in _dbContext.Users)
            {
                Console.WriteLine(item.TelegramUserId + " " + item.IsAdmin);
            }
            await _botClient.SendTextMessageAsync(chatId, $"Вы зарегистрированы в группе {groupName}");
        }

        private bool IsAdmin(long userId)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == userId);
            return user != null && user.IsAdmin;
        }

        private async Task ProcessAdminFile(long adminId, string fileId)
        {
            var file = await _botClient.GetFileAsync(fileId);
            var fileStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath, fileStream);

            var schedule = _excelService.ProcessExcelFile(fileStream);
            var scheduleService = new ScheduleService(schedule);

            var users = _dbContext.Users.ToList();
            foreach (var user in users)
            {
                if (user.IsAdmin == false)
                {
                    var scheduleMessage = scheduleService.GetScheduleForGroup(user.GroupName);
                    await _botClient.SendTextMessageAsync(user.TelegramUserId, scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown); //Отправка расписания для зарег. пользователоя
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
