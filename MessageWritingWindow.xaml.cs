using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RNA_Rebuild_Admin
{
	/// <summary>
	/// Interaction logic for MessageWritingWindow.xaml
	/// </summary>
	/// 

	public delegate void MessageSendingDelegate(string text, bool IsForAll);
	public partial class MessageWritingWindow : Window
	{
		public event MessageSendingDelegate MessageSendingEvent;
		private bool IsForAll = false;
		public MessageWritingWindow(bool IsForAll)
		{
			this.IsForAll = IsForAll;
			InitializeComponent();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			MessageSendingEvent?.BeginInvoke(MessageTextBox.Text, IsForAll, null, null);
			Close();
		}
	}
}
