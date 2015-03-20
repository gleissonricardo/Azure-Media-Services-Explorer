﻿//----------------------------------------------------------------------- 
// <copyright file="CreateLiveChannel.cs" company="Microsoft">Copyright (c) Microsoft Corporation. All rights reserved.</copyright> 
// <license>
// Azure Media Services Explorer Ver. 3.1
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//  
// http://www.apache.org/licenses/LICENSE-2.0 
//  
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License. 
// </license> 

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Net;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;



namespace AMSExplorer
{
    public partial class CreateLiveChannel : Form
    {
        private bool EncodingTabDisplayed = false;
        private bool InitPhase = true;
        private BindingList<AudioStream> audiostreams = new BindingList<AudioStream>();


        public string ChannelName
        {
            get { return textboxchannelname.Text; }
            set { textboxchannelname.Text = value; }
        }


        public string ChannelDescription
        {
            get { return textBoxDescription.Text; }
            set { textBoxDescription.Text = value; }
        }


        public ChannelEncodingType EncodingType
        {
            get
            {
                return (ChannelEncodingType)(Enum.Parse(typeof(ChannelEncodingType), (string)comboBoxEncodingType.SelectedItem));
            }
        }

        public ChannelEncoding EncodingOptions
        {
            get
            {
                ChannelEncoding encodingoption = new ChannelEncoding()
                {
                    SystemPreset = comboBoxEncodingPreset.Text,
                    AdMarkerSource = (AdMarkerSource)(Enum.Parse(typeof(AdMarkerSource), ((Item)comboBoxAdMarkerSource.SelectedItem).Value)),
                };
                if (comboBoxProtocolInput.Text == Enum.GetName(typeof(StreamingProtocol), StreamingProtocol.RTPMPEG2TS))
                { // RTP
                    List<VideoStream> videostreams = new List<VideoStream>();
                    videostreams.Add(new VideoStream() { Index = (int)numericUpDownVideoStreamIndex.Value });
                    encodingoption.VideoStreams = new ReadOnlyCollection<VideoStream>(videostreams);

                    List<AudioStream> audiostreamsl = new List<AudioStream>();
                    audiostreamsl.Add(new AudioStream() { Language = ((Item)comboBoxAudioLanguageMain.SelectedItem).Value, Index = (int)numericUpDownAudioIndexMain.Value });
                    audiostreamsl.AddRange(audiostreams);
                    encodingoption.AudioStreams = new ReadOnlyCollection<AudioStream>(audiostreamsl);

                }

                return encodingoption;
            }
        }

        public ChannelSlate Slate
        {
            get
            {
                ChannelSlate myslate = new ChannelSlate()
                {
                    InsertSlateOnAdMarker = checkBoxAdInsertSlate.Checked,
                    DefaultSlateAssetId = checkBoxAdInsertSlate.Checked ? textBoxSlateImage.Text : null

                };
                return myslate;
            }
        }

        public StreamingProtocol Protocol
        {
            get
            {
                return (StreamingProtocol)(Enum.Parse(typeof(StreamingProtocol), (string)comboBoxProtocolInput.SelectedItem));
            }
        }

        public short? HLSFragmentPerSegment
        {
            get
            {
                return checkBoxHLSFragPerSegDefined.Checked ? (short?)numericUpDownHLSFragPerSeg.Value : null;
            }
            set
            {
                if (value != null)
                    numericUpDownHLSFragPerSeg.Value = (short)value;
            }
        }

        public TimeSpan? KeyframeInterval
        {
            get
            {
                TimeSpan? ts = null;
                if (checkBoxKeyFrameIntDefined.Checked)
                {
                    try
                    {
                        ts = TimeSpan.FromSeconds(Convert.ToDouble(textBoxKeyFrame.Text));
                    }
                    catch
                    {
                    }
                }
                return ts;
            }
            set
            {
                textBoxKeyFrame.Text = value.Value.TotalSeconds.ToString();
            }
        }


        public List<IPRange> inputIPAllow
        {
            get
            {
                List<IPRange> ips = new List<IPRange>();
                IPRange ip;

                if (checkBoxRestrictIngestIP.Checked)
                {
                    ip = new IPRange() { Name = "default", Address = IPAddress.Parse(textBoxRestrictIngestIP.Text) };
                }
                else
                {
                    ip = new IPRange() { Name = "Allow All", Address = IPAddress.Parse("0.0.0.0"), SubnetPrefixLength = 0 };
                }
                ips.Add(ip);
                return ips;
            }
        }

        public List<IPRange> previewIPAllow
        {
            get
            {
                List<IPRange> ips = new List<IPRange>();
                IPRange ip;

                if (checkBoxRestrictPreviewIP.Checked)
                {
                    ip = new IPRange() { Name = "default", Address = IPAddress.Parse(textBoxRestrictPreviewIP.Text) };
                }
                else
                {
                    ip = new IPRange() { Name = "Allow All", Address = IPAddress.Parse("0.0.0.0"), SubnetPrefixLength = 0 };
                }
                ips.Add(ip);
                return ips;
            }
        }


        public bool StartChannelNow
        {
            get { return checkBoxStartChannel.Checked; }
            set { checkBoxStartChannel.Checked = value; }
        }

        public CreateLiveChannel()
        {
            InitializeComponent();
            this.Icon = Bitmaps.Azure_Explorer_ico;
        }

        private void CreateLiveChannel_Load(object sender, EventArgs e)
        {
            WarningChannelName.Text = string.Empty;

            FillComboProtocols(false);
           
            comboBoxEncodingType.Items.AddRange(Enum.GetNames(typeof(ChannelEncodingType)).ToArray()); // license type
            comboBoxEncodingType.SelectedItem = Enum.GetName(typeof(ChannelEncodingType), ChannelEncodingType.None);
            tabControlLiveChannel.TabPages.Remove(tabPageLiveEncoding);
            tabControlLiveChannel.TabPages.Remove(tabPageAudioOptions);
            tabControlLiveChannel.TabPages.Remove(tabPageAdConfig);

            comboBoxEncodingPreset.Items.Add("Default720p");
            comboBoxEncodingPreset.SelectedIndex = 0;

            labelWarningIngest.Text = string.Empty;
            labelWarningPreview.Text = string.Empty;



            dataGridViewAudioStreams.DataSource = audiostreams;
            dataGridViewAudioStreams.DataError += new DataGridViewDataErrorEventHandler(dataGridView_DataError);

            Item myitem = new Item("Undefined", "und");
            comboBoxAudioLanguageMain.Items.Add(myitem);
            comboBoxAudioLanguageMain.SelectedItem = myitem;
            comboBoxAudioLanguageAddition.Items.Add(myitem);
            comboBoxAudioLanguageAddition.SelectedItem = myitem;

            foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
            {
                myitem = new Item(ci.DisplayName, ci.ThreeLetterISOLanguageName);
                comboBoxAudioLanguageMain.Items.Add(myitem);
                comboBoxAudioLanguageAddition.Items.Add(myitem);
            }
            InitPhase = false;
        }
        void dataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {

            MessageBox.Show("Wrong format");
        }

        private void checkBoxRestrictIngestIP_CheckedChanged(object sender, EventArgs e)
        {
            textBoxRestrictIngestIP.Enabled = checkBoxRestrictIngestIP.Checked;
            if (!textBoxRestrictIngestIP.Enabled)
            {
                labelWarningIngest.Text = string.Empty;
            }
        }

        private void textBoxRestrictIngestIP_TextChanged(object sender, EventArgs e)
        {
            bool Error = false;

            try
            {
                IPRange ip = new IPRange() { Name = "default", Address = IPAddress.Parse(textBoxRestrictIngestIP.Text) };
            }
            catch
            {
                labelWarningIngest.Text = "IP address incorrect";
                Error = true;
            }
            if (!Error)
            {
                labelWarningIngest.Text = string.Empty;
            }
        }

        private void checkBoxHLSFragPerSegDefined_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDownHLSFragPerSeg.Enabled = checkBoxHLSFragPerSegDefined.Checked;
        }

        private void checkBoxKeyFrameIntDefined_CheckedChanged(object sender, EventArgs e)
        {
            textBoxKeyFrame.Enabled = checkBoxKeyFrameIntDefined.Checked;
        }

        private void comboBoxProtocolInput_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxAdMarkerSource.Items.Clear();
            comboBoxAdMarkerSource.Items.Add(new Item("API (default)", Enum.GetName(typeof(AdMarkerSource), AdMarkerSource.Api)));
            // SCTE-35 only available or RTP input
            if (comboBoxProtocolInput.Text == Enum.GetName(typeof(StreamingProtocol), StreamingProtocol.RTPMPEG2TS))
            { // RTP
                comboBoxAdMarkerSource.Items.Add(new Item("SCTE-35 Cue Messages", Enum.GetName(typeof(AdMarkerSource), AdMarkerSource.Scte35)));
                panelRTP.Enabled = panelAudioControl.Enabled = true;
            }
            else
            {
                panelRTP.Enabled = panelAudioControl.Enabled = false;
            }
            comboBoxAdMarkerSource.SelectedIndex = 0;
        }

        private void comboBoxEncodingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!InitPhase)
            {
                // let's display the encoding tab if encoding has been choosen
                if (comboBoxEncodingType.Text == Enum.GetName(typeof(ChannelEncodingType), ChannelEncodingType.None) && EncodingTabDisplayed)
                {
                    tabControlLiveChannel.TabPages.Remove(tabPageLiveEncoding);
                    tabControlLiveChannel.TabPages.Remove(tabPageAudioOptions);
                    tabControlLiveChannel.TabPages.Remove(tabPageAdConfig);
                    EncodingTabDisplayed = false;
                    FillComboProtocols(false);
                }
                else if (!EncodingTabDisplayed)
                {
                    tabControlLiveChannel.TabPages.Add(tabPageLiveEncoding);
                    tabControlLiveChannel.TabPages.Add(tabPageAudioOptions);
                    tabControlLiveChannel.TabPages.Add(tabPageAdConfig);
                    EncodingTabDisplayed = true;
                    FillComboProtocols(true);
                }
            }
        }

        private void FillComboProtocols(bool displayrtp)
        {
            comboBoxProtocolInput.Items.Clear();
            if (displayrtp)
            {
                comboBoxProtocolInput.Items.AddRange(Enum.GetNames(typeof(StreamingProtocol)).ToArray()); // license type
            }
            else
            {
                comboBoxProtocolInput.Items.Add(Enum.GetName(typeof(StreamingProtocol), StreamingProtocol.FragmentedMP4));
                comboBoxProtocolInput.Items.Add(Enum.GetName(typeof(StreamingProtocol), StreamingProtocol.RTMP));
            }
            comboBoxProtocolInput.SelectedItem = Enum.GetName(typeof(StreamingProtocol), StreamingProtocol.FragmentedMP4);
        }

        private void checkBoxRestrictPreviewIP_CheckedChanged(object sender, EventArgs e)
        {
            textBoxRestrictPreviewIP.Enabled = checkBoxRestrictPreviewIP.Checked;
            if (!textBoxRestrictPreviewIP.Enabled)
            {
                labelWarningPreview.Text = string.Empty;
            }
        }

        private void textBoxRestrictPreviewIP_TextChanged(object sender, EventArgs e)
        {
            bool Error = false;

            try
            {
                IPRange ip = new IPRange() { Name = "default", Address = IPAddress.Parse(textBoxRestrictPreviewIP.Text) };
            }
            catch
            {
                labelWarningPreview.Text = "IP address incorrect";
                Error = true;
            }
            if (!Error)
            {
                labelWarningPreview.Text = string.Empty;
            }
        }

        private void buttonAddAudioStream_Click(object sender, EventArgs e)
        {
            audiostreams.Add(new AudioStream() { Language = ((Item)comboBoxAudioLanguageAddition.SelectedItem).Value, Index = (int)numericUpDownAudioIndexAddition.Value });
        }

        private void textboxchannelname_TextChanged(object sender, EventArgs e)
        {
            if (textboxchannelname.TextLength > 0)
            {
                WarningChannelName.Text = (IsChannelNameValid(textboxchannelname.Text)) ? string.Empty : "Channel name is not valid";
            }
        }

        internal static bool IsChannelNameValid(string name)
        {
            Regex reg = new Regex(@"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,30}[a-zA-Z0-9])?$", RegexOptions.Compiled);
            return (reg.IsMatch(name));
        }

        private void buttonDelAddOption_Click(object sender, EventArgs e)
        {
            if (dataGridViewAudioStreams.SelectedRows.Count == 1)
            {
                audiostreams.RemoveAt(dataGridViewAudioStreams.SelectedRows[0].Index);
            }
        }

    }
}
