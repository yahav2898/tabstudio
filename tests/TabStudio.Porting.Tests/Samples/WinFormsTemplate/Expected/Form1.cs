using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using WinFormsApp1.Porting;
namespace WinFormsApp1
{
    public partial class Form1 : AppForm
    {
        public Form1()
        {
            InitializeComponent();
            InitializePortedEvents();
        }
    }
}
