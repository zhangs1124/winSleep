using System.Windows.Forms;

namespace winSleep
{
    public class UserActivityMessageFilter : IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
        private readonly Form1 _form;

        public UserActivityMessageFilter(Form1 form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_KEYDOWN)
            {
                _form.UpdateLastActivityTime();
                System.Diagnostics.Debug.WriteLine("Key pressed: " + m.Msg);
            }
            return false;
        }
    }
} 