namespace OpticEMS.Services.Windows
{
    public interface IWindowService
    {
        void Close();

        void Minimize();

        void MaximizeOrRestore();

        void Move();    
    }
}
