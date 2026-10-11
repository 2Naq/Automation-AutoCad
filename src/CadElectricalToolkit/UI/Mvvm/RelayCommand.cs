using System;
using System.Windows.Input;

namespace CadElectricalToolkit.UI.Mvvm
{
    /// <summary>
    /// Triển khai ICommand đa năng cho WPF MVVM Binding.
    /// Hỗ trợ cả command không tham số và command có tham số (CommandParameter).
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// Tạo RelayCommand không tham số.
        /// </summary>
        /// <param name="execute">Action thực thi khi command được gọi.</param>
        /// <param name="canExecute">Hàm kiểm tra command có thể thực thi hay không. Mặc định luôn true.</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute != null ? _ => canExecute() : null)
        {
        }

        /// <summary>
        /// Tạo RelayCommand có tham số (CommandParameter từ XAML).
        /// </summary>
        /// <param name="execute">Action nhận tham số object từ UI.</param>
        /// <param name="canExecute">Hàm kiểm tra command có thể thực thi hay không.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Buộc WPF kiểm tra lại trạng thái CanExecute của tất cả Command.
        /// Gọi khi trạng thái bên ngoài thay đổi ảnh hưởng đến CanExecute.
        /// </summary>
        public static void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
