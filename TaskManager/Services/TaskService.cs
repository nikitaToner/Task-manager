using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services
{
    public class TaskService
    {

        private static void Task_Parameters(SQLiteCommand command, TaskItem task)
        {
            command.Parameters.AddWithValue("@Name", task.Name ?? string.Empty);
            command.Parameters.AddWithValue("@Title", task.Title ?? string.Empty);
            command.Parameters.AddWithValue("@Category", task.Category ?? "Общее");
            command.Parameters.AddWithValue("@IsCompleted", task.IsCompleted ? 1 : 0);
            command.Parameters.AddWithValue("@AssignedToUserId", task.AssignedToUserId.HasValue ? (object)task.AssignedToUserId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@IsForAllWorkers", task.IsForAllWorkers ? 1 : 0);
        }

        public static User Read_User(SQLiteDataReader reader, int offset)
        {
            return new User
            {
                Id = reader.GetInt32(offset),
                Login = reader.GetString(offset + 1),
                Password = reader.GetString(offset + 2),
                Name = reader.GetString(offset + 3),
                Role = (UserRole)reader.GetInt32(offset + 4)
            };
        }

        private static TaskItem Read_Task(SQLiteDataReader reader)
        {
            var task = new TaskItem
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Title = reader.GetString(2),
                Category = reader.GetString(3),
                IsCompleted = reader.GetInt32(4) == 1,
                CreatedByUserId = reader.GetInt32(5),
                AssignedToUserId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                IsForAllWorkers = reader.GetInt32(7) == 1,
                CreatedByUser = Read_User(reader, 8)
            };

            if (!reader.IsDBNull(13)) task.AssignedToUser = Read_User(reader, 13);
            return task;
        }

        public List<TaskItem> Get_Tasks(User currentUser, string category = "Все")
        {
            var resultat = new List<TaskItem>();
            using (var connection = DbContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT t.Id, t.Name, t.Title, t.Category, t.IsCompleted,
       t.CreatedByUserId, t.AssignedToUserId, t.IsForAllWorkers,
       cu.Id, cu.Login, cu.Password, cu.Name, cu.Role,
       au.Id, au.Login, au.Password, au.Name, au.Role
FROM TaskItems t
JOIN Users cu ON cu.Id = t.CreatedByUserId
LEFT JOIN Users au ON au.Id = t.AssignedToUserId
WHERE (@IsManager = 1 OR t.CreatedByUserId = @CurrentUserId OR t.AssignedToUserId = @CurrentUserId OR t.IsForAllWorkers = 1)
  AND (@Category = 'Все' OR t.Category = @Category)
ORDER BY t.Category ASC, t.Id DESC;";
                command.Parameters.AddWithValue("@IsManager", currentUser.Role == UserRole.Manager ? 1 : 0);
                command.Parameters.AddWithValue("@CurrentUserId", currentUser.Id);
                command.Parameters.AddWithValue("@Category", string.IsNullOrWhiteSpace(category) ? "Все" : category);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) resultat.Add(Read_Task(reader));
                }
            }
            return resultat;
        }

        public void Update_Task(TaskItem editedTask, User currentUser)
        {
            var task = Task_Id(editedTask.Id);
            bool canEdit = Edit_Task(task, currentUser);

            bool canChangeStatus =
                canEdit ||
                task.AssignedToUserId == currentUser.Id ||
                task.IsForAllWorkers;

            if (!canEdit && !canChangeStatus)
                throw new InvalidOperationException("Нет прав на изменение этой задачи.");

            if (canEdit)
            {
                task.Name = editedTask.Name;
                task.Title = editedTask.Title;
                task.Category = editedTask.Category;

                task.AssignedToUserId =
                    currentUser.Role == UserRole.Manager
                    ? editedTask.AssignedToUserId
                    : currentUser.Id;

                task.IsForAllWorkers =
                    currentUser.Role == UserRole.Manager &&
                    editedTask.IsForAllWorkers;
            }

            if (canChangeStatus)
            {
                task.IsCompleted = editedTask.IsCompleted;
            }

            using (var connection = DbContext.CreateConnection())
            using (var command = new SQLiteCommand(@"
UPDATE TaskItems
SET Name = @Name,
    Title = @Title,
    Category = @Category,
    IsCompleted = @IsCompleted,
    AssignedToUserId = @AssignedToUserId,
    IsForAllWorkers = @IsForAllWorkers
WHERE Id = @Id;", connection))
            {
                command.Parameters.AddWithValue("@Id", task.Id);
                Task_Parameters(command, task);
                command.ExecuteNonQuery();
            }
        }

        private TaskItem Task_Id(int id)
        {
            using (var connection = DbContext.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT t.Id, t.Name, t.Title, t.Category, t.IsCompleted,
       t.CreatedByUserId, t.AssignedToUserId, t.IsForAllWorkers,
       cu.Id, cu.Login, cu.Password, cu.Name, cu.Role,
       au.Id, au.Login, au.Password, au.Name, au.Role
FROM TaskItems t
JOIN Users cu ON cu.Id = t.CreatedByUserId
LEFT JOIN Users au ON au.Id = t.AssignedToUserId
WHERE t.Id = @Id;";
                command.Parameters.AddWithValue("@Id", id);
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Read_Task(reader) : null;
                }
            }
        }
        
        public List<User> Get_Workes()
        {
            var result = new List<User>();
            using (var connection = DbContext.CreateConnection())
            using (var command = new SQLiteCommand("SELECT Id, Login, Password, Name, Role FROM Users WHERE Role = 2 ORDER BY Name;", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read()) result.Add(Read_User(reader, 0));
            }
            return result;
        }

        public void Delete_Task(TaskItem task, User currentUser)
        {
            var tasks = Task_Id(task.Id);
            if (tasks == null) return;

            using (var connection = DbContext.CreateConnection())
            using (var command = new SQLiteCommand("DELETE FROM TaskItems WHERE Id = @Id;", connection))
            {
                command.Parameters.AddWithValue("@Id", task.Id);
                command.ExecuteNonQuery();
            }
        }
        public void Add_Task(TaskItem task, User currentUser)
        {
            task.CreatedByUserId = currentUser.Id;

            if (currentUser.Role == UserRole.Worker)
            {
                task.AssignedToUserId = currentUser.Id;
                task.IsForAllWorkers = false;
            }

            using (var connection = DbContext.CreateConnection())
            using (var command = new SQLiteCommand(@"
INSERT INTO TaskItems (Name, Title, Category, IsCompleted, CreatedByUserId, AssignedToUserId, IsForAllWorkers)
VALUES (@Name, @Title, @Category, @IsCompleted, @CreatedByUserId, @AssignedToUserId, @IsForAllWorkers);", connection))
            {
                Task_Parameters(command, task);
                command.Parameters.AddWithValue("@CreatedByUserId", task.CreatedByUserId);
                command.ExecuteNonQuery();
            }
        }


        public List<string> Get_Categori(User currentUser)
        {
            var categories = Get_Tasks(currentUser)
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            categories.Insert(0, "Все");
            return categories;
        }

        public bool Edit_Task(TaskItem task, User currentUser)
        {
            return currentUser.Role == UserRole.Manager || task.CreatedByUserId == currentUser.Id;
        }
    }
}
