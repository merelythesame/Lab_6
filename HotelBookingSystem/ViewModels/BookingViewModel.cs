using HotelBookingSystem.Models;
using HotelBookingSystem.Services;
using HotelBookingSystem.Views;
using MvvmHelpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace HotelBookingSystem.ViewModels
{
    public class BookingViewModel : BaseViewModel
    {
        private readonly BookingService _bookingService;
        private readonly ILogger _logger;

        public ObservableCollection<Booking> Bookings { get; set; }

        private Booking? _selectedBooking;
        private int _selectedRoomId;
        private DateTime? _checkInDate;
        private DateTime? _checkOutDate;
        private Guest? _currentGuest;

        public DateTime? FilterFrom { get; set; }
        public DateTime? FilterTo { get; set; }
        public int? FilterRoomId { get; set; }

        public ICommand EditBookingCommand { get; }
        public ICommand CancelBookingCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand BookRoomCommand { get; }

        public int SelectedRoomId
        {
            get => _selectedRoomId;
            set => SetProperty(ref _selectedRoomId, value);
        }

        public DateTime? CheckInDate
        {
            get => _checkInDate;
            set => SetProperty(ref _checkInDate, value);
        }

        public DateTime? CheckOutDate
        {
            get => _checkOutDate;
            set => SetProperty(ref _checkOutDate, value);
        }

        public Guest? CurrentGuest
        {
            get => _currentGuest;
            set
            {
                _currentGuest = value;
                OnPropertyChanged();
                RaiseBookRoomCanExecuteChanged();
            }
        }

        public Booking? SelectedBooking
        {
            get => _selectedBooking;
            set
            {
                _selectedBooking = value;
                OnPropertyChanged();
                RaiseBookingCommandsCanExecuteChanged();
            }
        }

        public BookingViewModel(BookingService bookingService, ILogger logger)
        {
            _bookingService = bookingService;
            _logger = logger;

            Bookings = new ObservableCollection<Booking>(_bookingService.GetAllBookings());
            Rooms = new ObservableCollection<Room>(_bookingService.GetAvailableRooms());

            CancelBookingCommand = new RelayCommand(CancelBooking, () => SelectedBooking != null);
            EditBookingCommand = new RelayCommand(EditBooking, () => SelectedBooking != null);
            ApplyFilterCommand = new RelayCommand(ApplyFilter);
            BookRoomCommand = new RelayCommand(BookRoom, CanBookRoom);

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedRoomId) ||
                    e.PropertyName == nameof(CheckInDate) ||
                    e.PropertyName == nameof(CheckOutDate))
                {
                    RaiseBookRoomCanExecuteChanged();
                }
            };
        }

        private void EditBooking()
        {
            if (SelectedBooking == null) return;

            var window = new EditBookingWindow(SelectedBooking);
            if (window.ShowDialog() == true)
            {
                TryUpdateBooking(window.EditedBooking);
            }
        }

        private void TryUpdateBooking(Booking updatedBooking)
        {
            try
            {
                _bookingService.UpdateBooking(updatedBooking);
                UpdateBookingInList(updatedBooking);
                _logger.LogInfo($"Booking {updatedBooking.Id} updated.");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message, "Edit Error");
            }
        }

        private void UpdateBookingInList(Booking updatedBooking)
        {
            var existing = Bookings.FirstOrDefault(b => b.Id == updatedBooking.Id);
            if (existing != null)
            {
                existing.RoomId = updatedBooking.RoomId;
                existing.CheckInDate = updatedBooking.CheckInDate;
                existing.CheckOutDate = updatedBooking.CheckOutDate;
            }
        }

        private void CancelBooking()
        {
            if (SelectedBooking == null) return;

            _bookingService.CancelBooking(SelectedBooking.Id);
            RemoveBookingFromList(SelectedBooking);
            _logger.LogInfo($"Booking {SelectedBooking.Id} cancelled.");
            SelectedBooking = null;
        }

        private void RemoveBookingFromList(Booking booking)
        {
            var toRemove = Bookings.FirstOrDefault(b => b.Id == booking.Id);
            if (toRemove != null)
                Bookings.Remove(toRemove);
        }

        private void ApplyFilter()
        {
            var filtered = _bookingService.FilterBookings(FilterFrom, FilterTo, FilterRoomId);
            Bookings.Clear();
            foreach (var booking in filtered)
                Bookings.Add(booking);
        }

        private void BookRoom()
        {
            try
            {
                if (!AreDatesValid() || !IsRoomSelected() || !IsGuestPresent())
                    return;

                var booking = _bookingService.CreateBooking(SelectedRoomId, CurrentGuest!.Id, CheckInDate!.Value, CheckOutDate!.Value);
                Bookings.Add(booking);

                _logger.LogInfo($"Room {SelectedRoomId} booked from {CheckInDate.Value:d} to {CheckOutDate.Value:d}");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message, "Помилка бронювання");
            }
        }

        private bool AreDatesValid()
        {
            if (!CheckInDate.HasValue || !CheckOutDate.HasValue)
            {
                ShowError("Будь ласка, виберіть дати заїзду і виїзду.", "Помилка");
                return false;
            }
            return true;
        }

        private bool IsRoomSelected()
        {
            if (SelectedRoomId == 0)
            {
                ShowError("Будь ласка, виберіть кімнату.", "Помилка");
                return false;
            }
            return true;
        }

        private bool IsGuestPresent()
        {
            if (CurrentGuest == null)
            {
                ShowError("Гість не авторизований.", "Помилка");
                return false;
            }
            return true;
        }

        private void ShowError(string message, string caption) => MessageBox.Show(message, caption);

        private bool CanBookRoom()
        {
            return SelectedRoomId > 0 && CheckInDate.HasValue && CheckOutDate.HasValue && CheckInDate < CheckOutDate && CurrentGuest != null;
        }

        private void RaiseBookRoomCanExecuteChanged() => (BookRoomCommand as RelayCommand)?.RaiseCanExecuteChanged();
        private void RaiseBookingCommandsCanExecuteChanged()
        {
            (CancelBookingCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditBookingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
