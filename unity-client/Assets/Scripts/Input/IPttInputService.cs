namespace UnityIsh.Input
{
    public interface IPttInputService
    {
        void Bind(string channel, string keyCode);
        bool IsPressed(string channel);
    }
}
