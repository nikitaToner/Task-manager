using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskManager.Models;
using TaskManager.Data;
using System.Data.SQLite;
using System.Windows;
using Serilog;
using Serilog.Core;
using TaskManager.LoggerSystem;
namespace TaskManager.Services
{
    public class AuthServes
    {
        
        public User Login(string login, string password)
        {
            var log = Log.ForContext<AuthServes>();
            using (var connection = DbContext.CreateConnection())
            using (var command = new SQLiteCommand("SELECT Id, Login, Password, Name, Role FROM Users WHERE Login = @Login AND Password = @Password", connection))
            {
                command.Parameters.AddWithValue("@Login", login ?? string.Empty);
                command.Parameters.AddWithValue("@Password", password ?? string.Empty);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    log.Information("Новый юзеррр");
                    return new User
                    {
                        Id = reader.GetInt32(0),
                        Login = reader.GetString(1),
                        Password = reader.GetString(2),
                        Name = reader.GetString(3),
                        Role = (UserRole)reader.GetInt32(4),
                    };
                }
                
            }
        }
    }
}
