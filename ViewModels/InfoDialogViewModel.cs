namespace IAFTS.ViewModels
{
    public class InfoDialogViewModel
    {
        public string Message { get; set; }
        public InfoDialogViewModel(string message)
        {
            Message = message;
        }
    }
}
