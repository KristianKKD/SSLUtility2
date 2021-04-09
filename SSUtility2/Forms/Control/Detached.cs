﻿using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SSUtility2 {

    public partial class Detached : Form {

        public VideoSettings settings;
        public Detached secondView;

        Recorder recorderD;

        public bool recording = false;

        public AxAXVLC.AxVLCPlugin2 vlcPlayer;

        public Detached(bool attachSecond) {
            InitializeComponent();
            settings = new VideoSettings();
            settings.originalDetached = this;
            vlcPlayer = PlayerD_VLCPlayer;
            if (attachSecond) {
                var t = new Thread(AttachSecond);
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
            }
        }

        void AttachSecond() {
            try {
                if (MainForm.m.p_Control.InvokeRequired) {
                    MainForm.m.p_Control.Invoke(new MethodInvoker(this.AttachSecond));
                } else {
                    secondView = new Detached(false);
                    secondView.settings.originalDetached = this;
                    secondView.settings.CopyPlayerD(settings);
                    secondView.settings.Text = "Secondary Video Settings";
                    secondView.settings.isSecondary = true;
                    secondView.settings.tP_Secondary.Dispose();
                    secondView.settings.tP_Main.Text = "Secondary Player";

                    secondView.PlayerD_VLCPlayer.Dispose();
                    secondView.vlcPlayer = MainForm.m.Second_VLCPLayer;
                }
            } catch (Exception e) {
                MessageBox.Show("ATTACHSECOND\n" + e.ToString());
            }
        }

        public void StartStop() {
            if (settings.isPlaying) {
                StopPlaying();
            } else {
                StartPlaying(true);
            }
        }

        public void StopPlaying() {
            if (!settings.isPlaying)
                return;
            vlcPlayer.playlist.stop();
            vlcPlayer.playlist.items.clear();
            settings.isPlaying = false;
            if(!settings.isSecondary)
                MainForm.m.Menu_Video_StartStop.Text = "Start Video Playback";
        }

        public async Task StartPlaying(bool showErrors) {
            try {
                if (MainForm.m.lite) {
                    settings.isPlaying = true;
                    return;
                }

                if (await Play(showErrors, this).ConfigureAwait(false)) {
                    Invoke((MethodInvoker)delegate {
                        MainForm.m.Menu_Video_StartStop.Text = "Stop Video Playback";
                    });
                    if (this == MainForm.m.mainPlayer && showErrors) {
                        if (!secondView.settings.isPlaying) {
                            secondView.settings.CopyPlayerD(settings);
                            Play(false, secondView);
                        }
                    }

                    if (ConfigControl.autoReconnect.boolVal) {
                        MainForm.m.setPage.tB_IPCon_Adr.Text = settings.tB_PlayerD_Adr.Text;
                        ConfigControl.savedIP.UpdateValue(MainForm.m.setPage.tB_IPCon_Adr.Text);
                        AsyncCamCom.TryConnect(showErrors);
                    }
                } else {
                    StopPlaying();
                }
            } catch (Exception e) {
                Tools.ShowPopup("Failed to init stream!\nShow more?", "Stream Failed!", e.ToString());
                StopPlaying();
            }
        }

        public static async Task<bool> Play(bool showError, Detached player) {
            try {
                if (MainForm.m.lite) {
                    player.settings.isPlaying = true;
                    return true;
                }

                //if (!this.IsHandleCreated)
                //    this.CreateHandle();

                Uri combinedUrl = new Uri(VideoSettings.GetCombined(player.settings));

                //rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov

                bool parsed = IPAddress.TryParse(combinedUrl.Host, out IPAddress parsedIP);

                if (!ConfigControl.ignoreAddress.boolVal) {
                    if (showError && !player.settings.isSecondary) {
                        if (combinedUrl.Host == "" || !parsed) {
                            MessageBox.Show("Address is invalid!");
                            return false;
                        }
                        if (!OtherCamCom.PingAdr(parsedIP).Result) {
                            MessageBox.Show("Address had no RTSP stream attached!");
                            return false;
                        }
                    }
                }

                if (player.settings.isPlaying)
                    player.StopPlaying();

                player.vlcPlayer.playlist.add(combinedUrl.ToString(), null, ":avcodec -hw:network -caching="
                    + player.settings.tB_PlayerD_Buffering.Text);
                player.vlcPlayer.playlist.next();

                player.settings.isPlaying = true;

                return true;
            } catch (Exception e) {
                Tools.ShowPopup("Failed to play stream!\nShow more?", "Stream Failed!", e.ToString());
                return false;
            }
        }

        public static async Task EnableSecond(bool copySettings) {
            if (MainForm.m.lite)
                return;

            MainForm.m.Menu_Settings_Info.Visible = true;
            if(MainForm.m.mainPlayer.settings.tC_PlayerSettings.TabPages.Count == 1)
                MainForm.m.mainPlayer.settings.tC_PlayerSettings.TabPages.Add(MainForm.m.mainPlayer.settings.secondaryPage);

            MainForm.m.sP_Player.Show();
            MainForm.m.sP_Player.BringToFront();
            if(copySettings)
                MainForm.m.mainPlayer.secondView.settings.CopyPlayerD(MainForm.m.mainPlayer.settings);
            if (MainForm.m.mainPlayer.settings.isPlaying)
                Play(false, MainForm.m.mainPlayer.secondView);
        }

        public static async Task DisableSecond() {
            MainForm.m.Menu_Settings_Info.Visible = false;
            MainForm.m.mainPlayer.settings.tC_PlayerSettings.TabPages.Remove(MainForm.m.mainPlayer.settings.tP_Secondary);

            MainForm.m.sP_Player.Hide(); //here

            if (MainForm.m.mainPlayer.secondView.settings.isPlaying)
                MainForm.m.mainPlayer.secondView.StopPlaying();
        }

        public void SnapShot() {
            Tools.SaveSnap(this);
        }

        private void Menu_Settings_Click(object sender, EventArgs e) {
            settings.Show();
        }

        private void Menu_StartStop_Click(object sender, EventArgs e) {
            StartStop();
        }

        private void Menu_Snapshot_Click(object sender, EventArgs e) {
            Tools.SaveSnap(this);
        }

        private void Menu_Record_Click(object sender, EventArgs e) {
            (bool, Recorder) vals = MainForm.m.StopStartRec(recording, this, recorderD);
            recording = vals.Item1;
            recorderD = vals.Item2;
        }

        public void CustomSwap() {
            try {
                VideoSettings tempSettings = new VideoSettings();

                tempSettings.CopyPlayerD(settings, true); //temp save old main settings
                VideoSettings.CopySecondarySettingsMoveToMain(settings, settings); //move second to main
                VideoSettings.CopyPrimarySettingsMoveToSecondary(tempSettings, settings); //move old settings to second

                Play(true, this);
                Play(true, secondView);

                MainForm.m.setPage.tB_IPCon_Adr.Text = settings.tB_PlayerD_Adr.Text;
                AsyncCamCom.TryConnect();

                tempSettings.Dispose();
            } catch (Exception e) {
                MessageBox.Show("Swap Fail\n" + e.ToString());
            }
        }

        public async Task UpdateMode() {
            try {
                bool isCam = InfoPanel.i.isCamera;

                settings.cB_PlayerD_CamType.Text = ConfigControl.savedCamera.stringVal;
                settings.check_PlayerD_Manual.Checked = true;

                if (isCam) {
                    secondView.settings.CopyPlayerD(settings);
                }

                if (ConfigControl.savedCamera.stringVal.Contains("Thermal")) {
                    settings.tB_PlayerD_RTSP.Text = VideoSettings.thermalRTSP;

                    if (isCam) {
                        secondView.settings.cB_PlayerD_CamType.Text = "Daylight";
                        secondView.settings.tB_PlayerD_RTSP.Text = VideoSettings.dayRTSP;
                    }
                } else if (ConfigControl.savedCamera.stringVal.Contains("Daylight")) {
                    settings.tB_PlayerD_RTSP.Text = VideoSettings.dayRTSP;

                    if (isCam) {
                        secondView.settings.cB_PlayerD_CamType.Text = "Thermal";
                        secondView.settings.tB_PlayerD_RTSP.Text = VideoSettings.thermalRTSP;
                    }
                }

                Play(false, this);
                if (isCam && secondView.settings.isPlaying) {
                    secondView.settings.check_PlayerD_Manual.Checked = true;
                    Play(false, secondView);
                }
            } catch (Exception e) {
                MessageBox.Show("UPDATEMODE\n" + e.ToString());
            }
        }

    }
}
