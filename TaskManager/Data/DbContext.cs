using Serilog;
using System;
using System.Data.SQLite;
using System.IO;

namespace TaskManager.Data
{
    public class DbContext
    {
        private static readonly ILogger _logger;
        private const string DatabaseFileName = "taskmanager.db";
        private static bool _initialized;

        static DbContext()
        {
            // Инициализация логгера
            LoggerSystem.Logger.Initialize();
            _logger = Log.ForContext<DbContext>();
            _logger.Information("статический конструктор выполнен");
        }

        public static SQLiteConnection CreateConnection()
        {
            _logger.Information("попытка открыть соединение");
            try
            {
                Database_Create_ALPHA(); 
                var connection = new SQLiteConnection($"Data Source={DatabaseFileName};Version=3;");
                connection.Open();
                _logger.Information("соединение успешно открыто");
                return connection;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, " ошибка при открытии соединения");
                throw; 
            }
        }

        public static void Database_Create_ALPHA()
        {
            _logger.Information("(Database_Create_ALPHA) начало");
            if (_initialized)
            {
                _logger.Information("(Database_Create_ALPHA) БД уже инициализирована, пропускаем");
                return;
            }

            try
            {
                var shouldSeed = !File.Exists(DatabaseFileName);
                if (shouldSeed)
                {
                    _logger.Information("(Database_Create_ALPHA) файл БД не найден, создаём новый");
                    SQLiteConnection.CreateFile(DatabaseFileName);
                }

                using (var connection = new SQLiteConnection($"Data Source={DatabaseFileName};Version=3;"))
                {
                    connection.Open();
                    _logger.Information("Database_Create_ALPHA: соединение открыто, выполняем SQL создания таблиц");
                    ExecuteSql(connection, @"
                        CREATE TABLE IF NOT EXISTS Users (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Login TEXT NOT NULL UNIQUE,
                            Password TEXT NOT NULL,
                            Name TEXT NOT NULL,
                            Role INTEGER NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS TaskItems (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Name TEXT NOT NULL,
                            Title TEXT NOT NULL,
                            Category TEXT NOT NULL,
                            IsCompleted INTEGER NOT NULL DEFAULT 0,
                            CreatedByUserId INTEGER NOT NULL,
                            AssignedToUserId INTEGER NULL,
                            IsForAllWorkers INTEGER NOT NULL DEFAULT 0,
                            FOREIGN KEY(CreatedByUserId) REFERENCES Users(Id),
                            FOREIGN KEY(AssignedToUserId) REFERENCES Users(Id)
                        );");

                    MigrateOldDB(connection);

                    var usersCount = Convert.ToInt32(ExecuteScalar(connection, "SELECT COUNT(*) FROM Users;"));
                    if (shouldSeed || usersCount == 0)
                    {
                        _logger.Information("(Database_Create_ALPHA) необходимо заполнение начальными данными (shouldSeed={ShouldSeed}, usersCount={UsersCount})", shouldSeed, usersCount);
                        Seed(connection);
                    }
                }

                _initialized = true;
                _logger.Information("(Database_Create_ALPHA) инициализация БД завершена успешно");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database_Create_ALPHA) критическая ошибка при инициализации БД");
                throw;
            }
        }

        private static void MigrateOldDB(SQLiteConnection connection)
        {
            _logger.Information("(MigrateOldDB) запуск миграции.");
            try
            {
                AddColumn(connection, "TaskItems", "Name", "TEXT NOT NULL DEFAULT ''");
                AddColumn(connection, "TaskItems", "IsCompleted", "INTEGER NOT NULL DEFAULT 0");

                ExecuteSql(connection, "UPDATE TaskItems SET Name = Title WHERE (Name IS NULL OR Name = '') AND Title IS NOT NULL;");
                if (ColumnExists(connection, "TaskItems", "Status"))
                {
                    ExecuteSql(connection, "UPDATE TaskItems SET IsCompleted = 1 WHERE Status = 3;");
                }
                _logger.Information("(MigrateOldDB) миграция выполнена.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "(MigrateOldDB) ошибка при миграции схемы БД.");
                throw;
            }
        }

        private static void AddColumn(SQLiteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            try
            {
                if (ColumnExists(connection, tableName, columnName))
                {
                    _logger.Information("(AddColumn) колонка {ColumnName} в таблице {TableName} уже существует, пропускаем.", columnName, tableName);
                    return;
                }
                ExecuteSql(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
                _logger.Information("(AddColumn) колонка {ColumnName} добавлена в таблицу {TableName}.", columnName, tableName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "(AddColumn) ошибка при добавлении колонки {ColumnName} в таблицу {TableName}.", columnName, tableName);
                throw;
            }
        }

        private static bool ColumnExists(SQLiteConnection connection, string tableName, string columnName)
        {
            try
            {
                using (var command = new SQLiteCommand($"PRAGMA table_info({tableName});", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.GetString(1) == columnName)
                        {
                            _logger.Information("(ColumnExists) колонка {ColumnName} существует в таблице {TableName}.", columnName, tableName);
                            return true;
                        }
                    }
                }
                _logger.Information("(ColumnExists) колонка {ColumnName} не найдена в таблице {TableName}.", columnName, tableName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "(ColumnExists) ошибка при проверке существования колонки {ColumnName} в таблице {TableName}.", columnName, tableName);
                throw;
            }
        }

        private static void Seed(SQLiteConnection connection)
        {
            _logger.Information("(Seed) начальное заполнение таблиц.");
            try
            {
                ExecuteSql(connection, @"
                    INSERT OR IGNORE INTO Users (Login, Password, Name, Role) VALUES
                    ('manager', '1234', 'Руководитель', 1),
                    ('worker1', '1234', 'Работник', 2),
                    ('worker2', '1234', 'Работник 2', 2);");
                _logger.Information("(Seed) начальные данные успешно добавлены.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "(Seed) ошибка при вставке начальных данных.");
                throw;
            }
        }

        private static void ExecuteSql(SQLiteConnection connection, string sql)
        {
            try
            {
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.ExecuteNonQuery();
                }
                _logger.Information("(ExecuteSql) SQL выполнен успешно.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "(ExecuteSql) ошибка при выполнении SQL:\n{sql}", sql);
                throw;
            }
        }

        private static object ExecuteScalar(SQLiteConnection connection, string sql)
        {
            try
            {
                using (var command = new SQLiteCommand(sql, connection))
                {
                    var result = command.ExecuteScalar();
                    _logger.Information("(ExecuteScalar) SQL выполнен");
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "(ExecuteScalar) ошибка при выполнении SQL:\n{sql}", sql);
                throw;
            }
        }
    }
}
