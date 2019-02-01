using Microsoft.Win32;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TexodeApp.Client.Properties;

namespace TexodeApp.Client
{
    public class ViewModel : INotifyPropertyChanged
    {
        static readonly string apiUrl = Settings.Default.ApiUrl;
        static readonly string apiResource = "api/data";

        RestClient restClient;

        public ViewModel() {
            restClient = new RestClient(apiUrl);
        }

        void GetBooks() {
            var req = new RestRequest(apiResource, Method.GET);
            var resp = restClient.Execute<List<Book>>(req);

            if (resp.IsSuccessful) {
                Books = new ObservableCollection<Book>(resp.Data);
                ServerConnectionFlag = true;
                SelectedSortIndex = null;
            }
            else {
                ServerConnectionFlag = false;
                MessageBox.Show(resp.ErrorMessage ?? $"Server Error:\r\n{resp.StatusDescription}", "Something Went Wrong", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        IRestResponse<Book> PostBook(Book book) {
            var req = new RestRequest(apiResource, Method.POST);
            req.AddJsonBody(book);

            return restClient.Execute<Book>(req);
        }

        IRestResponse<Book> PutBook(Book book) {
            var req = new RestRequest(apiResource, Method.PUT);
            req.AddJsonBody(book);

            return restClient.Execute<Book>(req);
        }

        IRestResponse DeleteBooksByIds(IEnumerable<int> ids) {
            var req = new RestRequest(apiResource, Method.DELETE);
            req.AddJsonBody(ids);

            return restClient.Execute(req);
        }

        RelayCommand _RefreshCommand;
        public ICommand RefreshCommand {
            get => _RefreshCommand ?? (_RefreshCommand = new RelayCommand(param => GetBooks(), param => true));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool _ServerConnectionFlag;
        public bool ServerConnectionFlag {
            get => _ServerConnectionFlag;
            set { _ServerConnectionFlag = value; OnPropertyChanged(nameof(ServerConnectionFlag)); }
        }

        private ObservableCollection<Book> _Books;
        public ObservableCollection<Book> Books {
            get => _Books;
            set { _Books = value; OnPropertyChanged(nameof(Books)); }
        }

        private Book _SelectedBook;
        public Book SelectedBook {
            get => _SelectedBook;
            set {
                _SelectedBook = value;
                SelectedBookError = null;
                OnPropertyChanged(nameof(SelectedBook));
            }
        }

        private string _SelectedBookError;
        public string SelectedBookError {
            get { return _SelectedBookError; }
            set { _SelectedBookError = value; OnPropertyChanged(nameof(SelectedBookError)); }
        }

        private int? _SelectedSortIndex;
        public int? SelectedSortIndex {
            get => _SelectedSortIndex;
            set
            {
                _SelectedSortIndex = value;

                switch (value) {
                    case 0:
                        Books = new ObservableCollection<Book>(Books.OrderBy(x => x.Name));
                        break;

                    case 1:
                        Books = new ObservableCollection<Book>(Books.OrderByDescending(x => x.Name));
                        break;
                }

                OnPropertyChanged(nameof(SelectedSortIndex));
            }
        }

        RelayCommand _SelectAllBooksCommand;
        public ICommand SelectAllBooksCommand {
            get => _SelectAllBooksCommand ?? (_SelectAllBooksCommand = new RelayCommand(param => SelectAllBooks(), param => true));
        }

        void SelectAllBooks() {
            Books.ForEach(x => x.IsSelected = true);
        }
        
        RelayCommand _UnselectAllBooksCommand;
        public ICommand UnselectAllBooksCommand {
            get => _UnselectAllBooksCommand ?? (_UnselectAllBooksCommand = new RelayCommand(param => UnselectAllBooks(), param => true));
        }

        void UnselectAllBooks() {
            Books.ForEach(x => x.IsSelected = false);
        }

        RelayCommand _AddBookCommand;
        public ICommand AddBookCommand {
            get => _AddBookCommand ?? (_AddBookCommand = new RelayCommand(param => AddBook(), param => true));
        }

        void AddBook() {
            SelectedBook = new Book();
        }  

        RelayCommand _CloseBookCommand;
        public ICommand CloseBookCommand {
            get => _CloseBookCommand ?? (_CloseBookCommand = new RelayCommand(param => CloseBook(), param => true));
        }

        void CloseBook() {
            SelectedBook = null;
        }
        
        RelayCommand _ChooseBookImageCommand;
        public ICommand ChooseBookImageCommand {
            get => _ChooseBookImageCommand ?? (_ChooseBookImageCommand = new RelayCommand(param => ChooseBookImage(), param => true));
        }

        void ChooseBookImage() {
            var imageDialog = new OpenFileDialog();
            imageDialog.Filter = "Images (JPG, PNG)|*.JPG;*.PNG";
            imageDialog.CheckFileExists = true;
            imageDialog.Multiselect = false;

            if (imageDialog.ShowDialog() == true)
                SelectedBook.ImagePath = imageDialog.FileName;
        } 

        RelayCommand _SaveBookCommand;
        public ICommand SaveBookCommand {
            get => _SaveBookCommand ?? (_SaveBookCommand = new RelayCommand(param => SaveBook(), param => true));
        }

        void SaveBook() {
            SelectedBookError = null;

            if (string.IsNullOrWhiteSpace(SelectedBook.Name)) {
                SelectedBookError = "Please fill book name.";
                return;
            }

            if (SelectedBook.Id == 0) {
                if (string.IsNullOrWhiteSpace(SelectedBook.ImagePath)) {
                    SelectedBookError = "Please select book image.";
                    return;
                }
                
                var resp = PostBook(SelectedBook);

                if (resp.IsSuccessful) {
                    MessageBox.Show("The book was succesfully saved.");
                    CloseBook();
                }
                else
                    SelectedBookError = resp.ErrorMessage ?? $"Server Error:\r\n{resp.StatusDescription}";
            }
            else {
                if (SelectedBook.Name == Books.First(x => x.Id == SelectedBook.Id).Name && SelectedBook.ImagePath == null) {
                    CloseBook();
                    return;
                }

                var resp = PutBook(SelectedBook);

                if (resp.IsSuccessful) {
                    var book = Books.First(x => x.Id == SelectedBook.Id);

                    if (book.Name == resp.Data.Name)
                        book.Base64Image = resp.Data.Base64Image;
                    else {
                        if (SelectedSortIndex == null) {
                            book.Name = resp.Data.Name;
                            book.Base64Image = resp.Data.Base64Image;
                        }
                        else {
                            Books.Remove(book);

                            var index = Books.ToList().BinarySearch(resp.Data, new BookComparer(SelectedSortIndex == 1));
                            if (index < 0) index = ~index;
                            Books.Insert(index, resp.Data);
                        }
                    }

                    MessageBox.Show("The book was succesfully saved.");
                    CloseBook();
                }
                else
                    SelectedBookError = resp.ErrorMessage ?? $"Server Error:\r\n{resp.StatusDescription}";
            }
        }

        RelayCommand _DeleteSelectedBooksCommand;
        public ICommand DeleteSelectedBooksCommand {
            get => _DeleteSelectedBooksCommand ?? (_DeleteSelectedBooksCommand = new RelayCommand(param => DeleteSelectedBooks(), param => true));
        }

        void DeleteSelectedBooks() {
            var selectedBooks = Books.Where(x => x.IsSelected);

            if (!selectedBooks.Any()) {
                MessageBox.Show("There are no selected books.\r\nPlease select at least one book to delete.", "Bad Action", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dopString = selectedBooks.Count() > 10 ? "\n..." : string.Empty;
            var confirmDelete = MessageBox.Show($"Next books will be deleted ({selectedBooks.Count()}):\n\n{string.Join("\n", selectedBooks.Take(10).Select(x => $"\"{x.Name}\""))}{dopString}\n\nContinue?", "Confirm Action", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (confirmDelete != MessageBoxResult.Yes)
                return;

            var resp = DeleteBooksByIds(selectedBooks.Select(x => x.Id));

            if (resp.IsSuccessful)
                Books.RemoveAll(x => x.IsSelected);
            else
                MessageBox.Show(resp.ErrorMessage ?? $"Server Error:\r\n{resp.StatusDescription}", "Something Went Wrong", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        RelayCommand _EditSelectedBookCommand;
        public ICommand EditSelectedBookCommand {
            get => _EditSelectedBookCommand ?? (_EditSelectedBookCommand = new RelayCommand(param => EditSelectedBook(), param => true));
        }

        void EditSelectedBook() {
            var selectedBooks = Books.Where(x => x.IsSelected);

            if (selectedBooks.Count() != 1) {
                MessageBox.Show("Please select only one book.", "Bad Action", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SelectedBook = new Book { Id = selectedBooks.First().Id, Name = selectedBooks.First().Name, Base64Image = selectedBooks.First().Base64Image };
        }
    }

    public class Book : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public int Id { get; set; } = 0;

        private string _Name;
        public string Name {
            get => _Name;
            set { _Name = value; OnPropertyChanged(nameof(Name)); }
        }

        private string _Base64Image;
        public string Base64Image {
            get => _Base64Image;
            set { _Base64Image = value; OnPropertyChanged(nameof(Base64Image)); }
        }

        private string _ImageName;
        public string ImageName {
            get => Id != 0 && string.IsNullOrWhiteSpace(_ImageName) ? $"{Name.Replace(" ", "")}.img" : _ImageName;
            set { _ImageName = value; OnPropertyChanged(nameof(ImageName)); }
        }

        private string _ImagePath;
        public string ImagePath {
            get => _ImagePath;
            set {
                _ImagePath = value;
                ImageName = Path.GetFileName(value);
                Base64Image = Convert.ToBase64String(File.ReadAllBytes(value));
            }
        }

        private bool _IsSelected;
        public bool IsSelected {
            get => _IsSelected;
            set { _IsSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
    }

    public class BookComparer : IComparer<Book> {
        int descCoef = 1;

        public BookComparer(bool descFlag) {
            if (descFlag) descCoef = -1;
        }

        public int Compare(Book b1, Book b2) {
            return descCoef * string.Compare(b1.Name, b2.Name, false);
        }
    }

    public class RelayCommand : ICommand
    {
        #region Fields 
        readonly Action<object> _execute;
        readonly Predicate<object> _canExecute;
        #endregion // Fields 
        #region Constructors 
        public RelayCommand(Action<object> execute) : this(execute, null) { }
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            if (execute == null)
                throw new ArgumentNullException("execute");
            _execute = execute; _canExecute = canExecute;
        }
        #endregion // Constructors 
        #region ICommand Members 
        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            return _canExecute == null ? true : _canExecute(parameter);
        }
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public void Execute(object parameter) { _execute(parameter); }
        #endregion // ICommand Members 
    }
}
