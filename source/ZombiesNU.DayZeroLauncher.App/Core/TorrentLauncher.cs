﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using zombiesnu.DayZeroLauncher.App.Ui;

namespace zombiesnu.DayZeroLauncher.App.Core
{
	public class MetaAddon
	{
		[JsonProperty("addon")]
		public string Name;

		[JsonProperty("torrent")]
		public HashWebClient.RemoteFileInfo Torrent;

		[JsonProperty("name")]
		public string Description;

		[JsonProperty("version")]
		public string Version;

		[JsonProperty("installer")]
		public string InstallerName;
	}

	public class TorrentLauncher : BindableBase
	{
		private GameLauncher _gameLauncher = null;
		private TorrentUpdater _torrentUpdater = null;

		public TorrentLauncher(GameLauncher launcher)
		{
			_gameLauncher = launcher;
		}

		public void StartFromContentFile(string versionString, bool forceFullSystemsCheck, DayZUpdater updater)
		{
			if (string.IsNullOrWhiteSpace(versionString))
				return;

			string metaJsonFilename = MetaModDetails.GetFileName(versionString);
			if (!File.Exists(metaJsonFilename))
				return;

			if (IsRunning)
			{
				if (_updatingVersion.Equals(versionString, StringComparison.OrdinalIgnoreCase))
					return; //already running for this same version
			}

			_updatingVersion = versionString;
			Status = "Loading from local index...";
			ContinueFromContentFile(versionString, metaJsonFilename, forceFullSystemsCheck, updater, true);
		}

		private void ContinueFromContentFile(string versionString, string metaJsonFilename, bool forceFullSystemsCheck, DayZUpdater updater, bool errorMsgsOnly)
		{
			MetaModDetails modDetails = null;
			bool fullSystemCheck = true;
			try
			{
				modDetails = MetaModDetails.LoadFromFile(metaJsonFilename);
				if (!forceFullSystemsCheck)
				{
					CalculatedGameSettings.Current.Update();
					if (versionString.Equals(CalculatedGameSettings.Current.ModContentVersion, StringComparison.OrdinalIgnoreCase))
						fullSystemCheck = false;
				}
			}
			catch (Exception ex)
			{
				updater.Status = "Error parsing content index file";
				Status = ex.Message;
				IsRunning = false;
				_gameLauncher.SetModDetails(null, false, ex);
			}

			//start new torrents if needed
			if (modDetails != null)
			{
				_gameLauncher.SetModDetails(modDetails);

				//stop existing torrents going on
				if (_torrentUpdater != null)
				{
					TorrentUpdater.StopAllTorrents();
					_torrentUpdater = null;
				}
				_torrentUpdater = new TorrentUpdater(versionString, modDetails.AddOns, fullSystemCheck, this, updater, errorMsgsOnly); //this automatically starts it's async thread
			}
		}

		public void StartFromNetContent(string versionString, bool forceFullSystemsCheck, HashWebClient.RemoteFileInfo jsonIndex, DayZUpdater updater, bool errorMsgsOnly = false)
		{
			if (jsonIndex == null)
			{
				MessageBox.Show("No version index specified, please Check first.", "Error initiating torrent download", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (string.IsNullOrWhiteSpace(versionString))
			{
				MessageBox.Show("Invalid version specified for download", "Error initiating torrent download", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			if (IsRunning)
			{
				if (_updatingVersion.Equals(versionString, StringComparison.OrdinalIgnoreCase))
					return; //already running for this same version
			}				

			_updatingVersion = versionString;
			if (!errorMsgsOnly)
				updater.Status = DayZeroLauncherUpdater.STATUS_DOWNLOADING;

			Status = "Initiating Download...";

			string metaJsonFilename = MetaModDetails.GetFileName(versionString);
			var wc = new HashWebClient();
			wc.DownloadFileCompleted += (sender, args) =>
				{
					if (args.Cancelled)
					{
						Status = updater.Status = "Async operation cancelled";
						IsRunning = false;
						_gameLauncher.SetModDetails(null,true,null);
					}
					else if (args.Error != null)
					{
						updater.Status = "Error downloading content index file";
						Status = args.Error.Message;
						IsRunning = false;
						_gameLauncher.SetModDetails(null, false, args.Error);
					}
					else
						ContinueFromContentFile(versionString, metaJsonFilename, forceFullSystemsCheck, updater, errorMsgsOnly);
				};
			wc.BeginDownload(jsonIndex, metaJsonFilename);
		}

		private string _updatingVersion = null;
        public bool IsRunning
		{
			get { return (!string.IsNullOrEmpty(_updatingVersion)); }
			set
			{
				if (value != false)
					throw new ArgumentException("IsRunning");

				_updatingVersion = null;
				PropertyHasChanged("IsRunning");
			}
		}

		public bool RunningForVersion(string wantedVersion)
		{
			if (!IsRunning)
				return false;

			return _updatingVersion.Equals(wantedVersion, StringComparison.OrdinalIgnoreCase);
		}

		private string _status;
		public string Status
		{
			get { return _status; }
			set
			{
				_status = value;
				Execute.OnUiThread(() => PropertyHasChanged("Status"));
			}
		}
	}
}