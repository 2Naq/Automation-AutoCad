using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CadElectricalToolkit.UI.Mvvm
{
    /// <summary>
    /// Lớp nền cho tất cả ViewModel trong kiến trúc MVVM.
    /// Cung cấp cơ chế thông báo thay đổi thuộc tính (INotifyPropertyChanged)
    /// và phương thức tiện ích SetProperty để cập nhật giá trị + tự động raise event.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gửi thông báo thay đổi thuộc tính đến UI (WPF Binding Engine).
        /// </summary>
        /// <param name="propertyName">
        /// Tên thuộc tính thay đổi. Mặc định là CallerMemberName (tự lấy tên hàm gọi).
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Cập nhật giá trị backing field và tự động raise PropertyChanged nếu giá trị thay đổi.
        /// Trả về true nếu giá trị thực sự đã thay đổi, false nếu không đổi.
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của property.</typeparam>
        /// <param name="field">Tham chiếu đến backing field.</param>
        /// <param name="value">Giá trị mới.</param>
        /// <param name="propertyName">Tên property (tự động lấy từ CallerMemberName).</param>
        /// <returns>true nếu giá trị đã thay đổi.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
