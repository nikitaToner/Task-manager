using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TaskManager.Commands;
using TaskManager.Models;
using TaskManager.Services;



namespace TaskManager.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly AuthServices _authService = new Auth();
        private readonly TaskService _taskService = new Task_service();

        private User _currentUser;
        private TaskItem _selectedTask;
        private User _selectedWorker;
        private string _login;
        private string _password;
        private string _name;
        private string _title;
        private string _category = "Общее";
        private string _selectedCategory = "Все";
        private bool _isCompleted;
        private bool _isForAllWorkers;
        private string _message;

        public ObservableCollection<TaskItem> Tasks { get; } = new ObservableCollection<TaskItem>();
        public ObservableCollection<User> Workers { get; } = new ObservableCollection<User>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();

        public string Login { get => _login; set { _login = value; OnPropertyChanged(); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
        public User CurrentUser { get => _currentUser; set { _currentUser = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAuthenticated)); OnPropertyChanged(nameof(IsManager)); OnPropertyChanged(nameof(CurrentUserInfo)); } }
        public bool IsAuthenticated => CurrentUser != null;
        public bool IsManager => CurrentUser != null && CurrentUser.Role == UserRole.Manager;
        public string CurrentUserInfo => CurrentUser == null ? "Вы не вошли" : $"{CurrentUser.Name} ({CurrentUser.Role})";

        public TaskItem SelectedTask
        {
            get => _selectedTask;
            set
            {
                _selectedTask = value;
                OnPropertyChanged();
                if (value != null) FillForm(value);
            }
        }

        public User SelectedWorker { get => _selectedWorker; set { _selectedWorker = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }
        public bool IsCompleted { get => _isCompleted; set { _isCompleted = value; OnPropertyChanged(); } }
        public bool IsForAllWorkers { get => _isForAllWorkers; set { _isForAllWorkers = value; OnPropertyChanged(); } }
        public string SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); LoadTasks(); } }
        public string Message { get => _message; set { _message = value; OnPropertyChanged(); } }

        public ICommand LoginCommand { get; set; }
        public ICommand LogoutCommand { get; set; }
        public ICommand AddCommand { get; set; }
        public ICommand UpdateCommand { get; set; }
        public ICommand DeleteCommand { get; set; }
        public ICommand ClearCommand { get; set; }
        public ICommand RefreshCommand { get; set; }

        public MainViewModel()
        {
            LoginCommand = new Command(_ => LoginUser());
            LogoutCommand = new Command(_ => Logout(), _ => IsAuthenticated);
            AddCommand = new Command(_ => AddTask(), _ => IsAuthenticated);
            UpdateCommand = new Command(_ => UpdateTask(), _ => IsAuthenticated && SelectedTask != null);
            DeleteCommand = new Command(_ => DeleteTask(), _ => IsAuthenticated && SelectedTask != null);
            ClearCommand = new Command(_ => ClearForm());
            RefreshCommand = new Command(_ => RefreshAll(), _ => IsAuthenticated);
        }
        private void UpdateTask()
        {
            if (SelectedTask == null) return;
            if (!ValidateForm()) return;

            try
            {
                var task = BuildTaskFromForm();
                task.Id = SelectedTask.Id;
                _taskService.Update_Task(task, CurrentUser);
                Message = "Задача обновлена.";
                ClearForm();
                RefreshAll();
            }
            catch (Exception ex)
            {
                Message = ex.Message;
            }
        }

        private void LoginUser()
        {
            CurrentUser = _authService.Login(Login, Password);
            if (CurrentUser == null)
            {
                Message = "Неверный логин или пароль.";
                Console.Write(Message);
                return;
            }

            Message = "Вход выполнен.";
            RefreshAll();
        }

        private void Logout()
        {
            CurrentUser = null;
            Tasks.Clear();
            Workers.Clear();
            Categories.Clear();
            ClearForm();
            Message = "Выход выполнен.";
        }

        private void RefreshAll()
        {
            LoadWorkers();
            LoadCategories();
            LoadTasks();
        }

        private void LoadTasks()
        {
            if (!IsAuthenticated) return;
            Tasks.Clear();
            foreach (var task in _taskService.Get_Tasks(CurrentUser, SelectedCategory)) Tasks.Add(task);
        }

        private void LoadWorkers()
        {
            Workers.Clear();
            foreach (var worker in _taskService.Get_Workes()) Workers.Add(worker);
        }

        private void LoadCategories()
        {
            var old = SelectedCategory;
            Categories.Clear();
            foreach (var category in _taskService.Get_Categori(CurrentUser)) Categories.Add(category);
            SelectedCategory = Categories.Contains(old) ? old : "Все";
        }

        private void AddTask()
        {
            if (!ValidateForm()) return;
            try
            {
                _taskService.Add_Task(BuildTaskFromForm(), CurrentUser);
                Message = "Задача создана.";
                ClearForm();
                RefreshAll();
            }
            catch (Exception ex) { Message = ex.Message; }
        }



        private void DeleteTask()
        {
            try
            {
                _taskService.Delete_Task(SelectedTask, CurrentUser);
                Message = "Задача удалена.";
                ClearForm();
                RefreshAll();
            }
            catch (Exception ex) { Message = ex.Message; }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(Name)) { Message = "Введите имя задачи."; return false; }
            if (string.IsNullOrWhiteSpace(Title)) { Message = "Введите заголовок задачи."; return false; }
            if (string.IsNullOrWhiteSpace(Category)) { Message = "Введите категорию."; return false; }
            if (IsManager && !IsForAllWorkers && SelectedWorker == null) { Message = "Выберите работника или отметьте 'для всех'."; return false; }
            return true;
        }

        private TaskItem BuildTaskFromForm()
        {
            return new TaskItem
            {
                Name = Name,
                Title = Title,
                Category = Category,
                IsCompleted = IsCompleted,
                AssignedToUserId = IsManager && !IsForAllWorkers ? SelectedWorker?.Id : CurrentUser.Id,
                IsForAllWorkers = IsManager && IsForAllWorkers
            };
        }

        private void FillForm(TaskItem task)
        {
            Name = task.Name;
            Title = task.Title;
            Category = task.Category;
            IsCompleted = task.IsCompleted;
            IsForAllWorkers = task.IsForAllWorkers;
            SelectedWorker = Workers.FirstOrDefault(w => w.Id == task.AssignedToUserId);
        }

        private void ClearForm()
        {
            SelectedTask = null;
            Name = string.Empty;
            Title = string.Empty;
            Category = "Общее";
            IsCompleted = false;
            IsForAllWorkers = false;
            SelectedWorker = Workers.FirstOrDefault();
        }
    }
}
