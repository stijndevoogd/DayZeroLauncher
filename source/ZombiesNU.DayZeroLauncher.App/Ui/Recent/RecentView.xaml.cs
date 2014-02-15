﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using zombiesnu.DayZeroLauncher.App.Core;

namespace zombiesnu.DayZeroLauncher.App.Ui.Recent
{
	/// <summary>
	/// Interaction logic for RecentView.xaml
	/// </summary>
	public partial class RecentView : UserControl
	{
		public RecentView()
		{
			InitializeComponent();
		}

		private void RowDoubleClick(object sender, MouseButtonEventArgs e)
		{			
			var recentServer = (RecentServer) TheList.SelectedItem;
			if(recentServer == null)
				return;

			GameLauncher.JoinServer(MainWindow.GetWindow(this.Parent),recentServer.Server);
		}
	}
}
