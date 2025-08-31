using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using Draggy.Services;

namespace Draggy.ViewModels
{
    public class ShelfViewModel : INotifyPropertyChanged
    {
        private static ShelfViewModel? _instance;
        public static ShelfViewModel Instance => _instance ??= new ShelfViewModel();

        public ObservableCollection<ShelfItem> Items { get; } = new();

        public ICommand StartDragCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand ClearAllCommand { get; }

        private ShelfViewModel()
        {
            StartDragCommand = new RelayCommand<ShelfItem>(StartDrag);
            RemoveItemCommand = new RelayCommand<ShelfItem>(RemoveItem);
            ClearAllCommand = new RelayCommand(ClearAll);
        }

        public void AddItem(string filePath)
        {
            try
            {
                var item = new ShelfItem
                {
                    FilePath = filePath,
                    Thumbnail = ShellThumbnail.GetThumbnail(filePath, 48)
                };
                
                // Evita duplicati
                if (!Items.Any(x => x.FilePath == filePath))
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nell'aggiungere item: {ex.Message}");
            }
        }

        private void StartDrag(ShelfItem? item)
        {
            if (item?.FilePath != null && File.Exists(item.FilePath))
            {
                var data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new string[] { item.FilePath });
                System.Windows.DragDrop.DoDragDrop(App.Current.MainWindow, data, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);
            }
        }

        private void RemoveItem(ShelfItem? item)
        {
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        private void ClearAll()
        {
            Items.Clear();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Comando helper semplice
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
    }
}
