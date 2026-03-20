using System;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp3
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = @"Local\FreeEnter_SingleInstance_BFC5D84C";

        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "已有相同程序在运行。",
                        "FreeEnter",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FreeEnter());
            }
        }
    }
}
