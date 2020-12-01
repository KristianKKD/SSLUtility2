﻿using System;
using System.Windows.Forms;

namespace SSLUtility2
{
    public partial class ResponseLog : Form {

        public ResponseLog() {
            InitializeComponent();
        }

        private void b_RL_Clear_Click(object sender, EventArgs e) {
            rtb_Log.Clear();
        }

        private void b_RL_Save_Click(object sender, EventArgs e) {
            PelcoD.SaveFile(rtb_Log.Lines, "ResponseLog");
        }

        private void ResponseLog_FormClosing(object sender, FormClosingEventArgs e) {
            if (e.CloseReason == CloseReason.UserClosing) {
                e.Cancel = true;
                Hide();
            }
        }

    }
}
