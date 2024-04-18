﻿using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using MessageBox = Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class GenerateVideoWithHardSubs : Form
    {
        private bool _abort;
        private bool _loading;
        private readonly Subtitle _assaSubtitle;
        private VideoInfo _videoInfo;
        private readonly string _inputVideoFileName;
        private static readonly Regex FrameFinderRegex = new Regex(@"[Ff]rame=\s*\d+", RegexOptions.Compiled);
        private long _processedFrames;
        private long _startTicks;
        private long _totalFrames;
        private StringBuilder _log;
        private readonly bool _isAssa;
        private readonly FfmpegMediaInfo _mediaInfo;
        private bool _promptFFmpegParameters;
        private readonly bool _mpvOn;
        private readonly string _mpvSubtitleFileName;
        private readonly bool _noSubtitles;
        private bool _converting;
        public string VideoFileName { get; private set; }
        public long MillisecondsEncoding { get; private set; }
        private PreviewVideo _previewVideo;
        public bool BatchMode { get; set; }
        public string BatchInfo { get; set; }
        private readonly List<BatchVideoAndSub> _batchVideoAndSubList;

        public class BatchVideoAndSub
        {
            public string VideoFileName { get; set; }
            public string SubtitleFileName { get; set; }
            public long VideoFileSizeInBytes { get; set; }
            public long SubtitleFileFileSizeInBytes { get; set; }
        }

        public GenerateVideoWithHardSubs(Subtitle assaSubtitle, SubtitleFormat format, string inputVideoFileName, VideoInfo videoInfo, int? fontSize, bool setStartEndCut)
        {
            UiUtil.PreInitialize(this);
            InitializeComponent();
            textBoxLog.ScrollBars = ScrollBars.Both;
            UiUtil.FixFonts(this);

            _loading = true;
            _videoInfo = videoInfo;
            _assaSubtitle = new Subtitle(assaSubtitle);
            _inputVideoFileName = inputVideoFileName;
            _batchVideoAndSubList = new List<BatchVideoAndSub>();

            _noSubtitles = _assaSubtitle == null ||
                           _assaSubtitle.Paragraphs.Count == 0 ||
                           (_assaSubtitle.Paragraphs.Count == 1 && string.IsNullOrWhiteSpace(_assaSubtitle.Paragraphs[0].Text));

            if (format.GetType() == typeof(NetflixImsc11Japanese))
            {
                _assaSubtitle = new Subtitle();
                var raw = NetflixImsc11JapaneseToAss.Convert(assaSubtitle, _videoInfo.Width, _videoInfo.Height);
                new AdvancedSubStationAlpha().LoadSubtitle(_assaSubtitle, raw.SplitToLines(), null);
                fontSize = null;
            }

            Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Title;
            buttonGenerate.Text = LanguageSettings.Current.Watermark.Generate;
            labelPleaseWait.Text = LanguageSettings.Current.General.PleaseWait;
            labelResolution.Text = LanguageSettings.Current.SubStationAlphaProperties.Resolution;
            labelPreviewPleaseWait.Text = LanguageSettings.Current.General.PleaseWait;
            labelFontSize.Text = LanguageSettings.Current.ExportPngXml.FontSize;
            labelSubtitleFont.Text = LanguageSettings.Current.ExportPngXml.FontFamily;
            buttonCancel.Text = LanguageSettings.Current.General.Cancel;
            labelAudioEnc.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Encoding;
            labelVideoEncoding.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Encoding;
            labelAudioBitRate.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.BitRate;
            labelAudioSampleRate.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.SampleRate;
            checkBoxMakeStereo.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Stereo;
            labelCRF.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Crf;
            labelPreset.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Preset;
            labelTune.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TuneFor;
            buttonPreview.Text = LanguageSettings.Current.General.Preview;
            checkBoxRightToLeft.Text = LanguageSettings.Current.Settings.FixRTLViaUnicodeChars;
            checkBoxAlignRight.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.AlignRight;
            checkBoxBox.Text = LanguageSettings.Current.SubStationAlphaStyles.OpaqueBox;
            buttonMode.Text = LanguageSettings.Current.AudioToText.BatchMode;
            deleteToolStripMenuItem.Text = LanguageSettings.Current.DvdSubRip.Remove;
            clearToolStripMenuItem.Text = LanguageSettings.Current.DvdSubRip.Clear;
            addFilesToolStripMenuItem.Text = LanguageSettings.Current.DvdSubRip.Add;
            buttonRemoveFile.Text = LanguageSettings.Current.DvdSubRip.Remove;
            buttonClear.Text = LanguageSettings.Current.DvdSubRip.Clear;
            buttonAddFile.Text = LanguageSettings.Current.DvdSubRip.Add;

            progressBar1.Visible = false;
            labelPleaseWait.Visible = false;
            labelPreviewPleaseWait.Visible = false;
            labelProgress.Text = string.Empty;
            labelFileName.Text = string.Empty;
            labelPass.Text = string.Empty;
            comboBoxVideoEncoding.SelectedIndex = 0;
            comboBoxCrf.SelectedIndex = 6;
            comboBoxAudioEnc.SelectedIndex = 0;
            comboBoxAudioSampleRate.SelectedIndex = 1;
            comboBoxTune.SelectedIndex = 0;
            comboBoxAudioBitRate.Text = "128k";
            checkBoxTargetFileSize_CheckedChanged(null, null);
            checkBoxTargetFileSize.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TargetFileSize;
            labelFileSize.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.FileSizeMb;
            numericUpDownTargetFileSize.Left = labelFileSize.Left + labelFileSize.Width + 5;
            linkLabelHelp.Text = LanguageSettings.Current.Main.Menu.Help.Title;
            groupBoxSettings.Text = LanguageSettings.Current.Settings.Title;
            groupBoxVideo.Text = LanguageSettings.Current.Main.Menu.Video.Title;
            groupBoxAudio.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Audio;
            promptParameterBeforeGenerateToolStripMenuItem.Text = LanguageSettings.Current.GenerateBlankVideo.GenerateWithFfmpegParametersPrompt;

            comboBoxVideoEncoding.Text = Configuration.Settings.Tools.GenVideoEncoding;
            comboBoxCrf.Text = Configuration.Settings.Tools.GenVideoCrf;
            comboBoxTune.Text = Configuration.Settings.Tools.GenVideoTune;
            comboBoxAudioEnc.Text = Configuration.Settings.Tools.GenVideoAudioEncoding;
            comboBoxAudioSampleRate.Text = Configuration.Settings.Tools.GenVideoAudioSampleRate;
            checkBoxMakeStereo.Checked = Configuration.Settings.Tools.GenVideoAudioForceStereo;
            checkBoxTargetFileSize.Checked = Configuration.Settings.Tools.GenVideoTargetFileSize;
            checkBoxBox.Checked = Configuration.Settings.Tools.GenVideoNonAssaBox;
            checkBoxAlignRight.Checked = Configuration.Settings.Tools.GenVideoNonAssaAlignRight;
            checkBoxRightToLeft.Checked = Configuration.Settings.Tools.GenVideoNonAssaAlignRight;

            labelVideoBitrate.Text = string.Empty;

            if (_videoInfo != null)
            {
                numericUpDownWidth.Value = _videoInfo.Width;
                numericUpDownHeight.Value = _videoInfo.Height;
            }

            var left = Math.Max(Math.Max(labelResolution.Left + labelResolution.Width, labelFontSize.Left + labelFontSize.Width), labelSubtitleFont.Left + labelSubtitleFont.Width) + 5;
            numericUpDownFontSize.Left = left;
            comboBoxSubtitleFont.Left = left;
            numericUpDownWidth.Left = left;
            labelX.Left = numericUpDownWidth.Left + numericUpDownWidth.Width + 3;
            numericUpDownHeight.Left = labelX.Left + labelX.Width + 3;
            buttonVideoChooseStandardRes.Left = numericUpDownHeight.Left + numericUpDownHeight.Width + 9;
            labelInfo.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.InfoAssaOff;
            checkBoxRightToLeft.Left = left;
            checkBoxAlignRight.Left = checkBoxRightToLeft.Right + 15;
            checkBoxBox.Left = numericUpDownFontSize.Right + 22;
            buttonOutlineColor.Left = checkBoxBox.Right + 2;
            buttonOutlineColor.Text = LanguageSettings.Current.AssaSetBackgroundBox.BoxColor;
            panelOutlineColor.Left = buttonOutlineColor.Right + 3;
            buttonForeColor.Left = buttonOutlineColor.Left;
            panelForeColor.Left = panelOutlineColor.Left;
            buttonForeColor.Text = LanguageSettings.Current.Settings.WaveformTextColor;

            var audioLeft = Math.Max(Math.Max(labelAudioEnc.Left + labelAudioEnc.Width, labelAudioSampleRate.Left + labelAudioSampleRate.Width), labelAudioBitRate.Left + labelAudioBitRate.Width) + 5;
            comboBoxAudioEnc.Left = audioLeft;
            checkBoxMakeStereo.Left = audioLeft;
            comboBoxAudioSampleRate.Left = audioLeft;
            comboBoxAudioBitRate.Left = audioLeft;

            linkLabelHelp.Left = Width - linkLabelHelp.Width - 30;

            numericUpDownCutFromMinutes.Left = numericUpDownCutFromHours.Right + 1;
            numericUpDownCutFromSeconds.Left = numericUpDownCutFromMinutes.Right + 1;

            numericUpDownCutToMinutes.Left = numericUpDownCutToHours.Right + 1;
            numericUpDownCutToSeconds.Left = numericUpDownCutToMinutes.Right + 1;

            _isAssa = !fontSize.HasValue;
            if (fontSize.HasValue && !_noSubtitles)
            {
                if (fontSize.Value < numericUpDownFontSize.Minimum)
                {
                    numericUpDownFontSize.Value = numericUpDownFontSize.Minimum;
                }
                else if (fontSize.Value > numericUpDownFontSize.Maximum)
                {
                    numericUpDownFontSize.Value = numericUpDownFontSize.Maximum;
                }
                else
                {
                    numericUpDownFontSize.Value = fontSize.Value;
                }

                checkBoxRightToLeft.Checked = Configuration.Settings.General.RightToLeftMode &&
                                              LanguageAutoDetect.CouldBeRightToLeftLanguage(_assaSubtitle);
            }
            else
            {
                numericUpDownFontSize.Enabled = false;
                labelFontSize.Enabled = false;
                labelSubtitleFont.Enabled = false;
                comboBoxSubtitleFont.Enabled = false;
                checkBoxRightToLeft.Left = checkBoxTargetFileSize.Left;
                checkBoxAlignRight.Enabled = false;
                checkBoxBox.Enabled = false;
                labelInfo.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.InfoAssaOn;
                buttonForeColor.Enabled = false;
                buttonOutlineColor.Enabled = false;
                panelOutlineColor.Enabled = false;
                panelForeColor.Enabled = false;
            }

            var initialFont = Configuration.Settings.Tools.GenVideoFontName;
            if (string.IsNullOrEmpty(initialFont))
            {
                initialFont = Configuration.Settings.Tools.ExportBluRayFontName;
            }
            if (string.IsNullOrEmpty(initialFont))
            {
                initialFont = UiUtil.GetDefaultFont().Name;
            }

            foreach (var x in FontHelper.GetRegularOrBoldCapableFontFamilies())
            {
                comboBoxSubtitleFont.Items.Add(x.Name);
                if (x.Name.Equals(initialFont, StringComparison.OrdinalIgnoreCase))
                {
                    comboBoxSubtitleFont.SelectedIndex = comboBoxSubtitleFont.Items.Count - 1;
                }
            }
            if (comboBoxSubtitleFont.SelectedIndex < 0 && comboBoxSubtitleFont.Items.Count > 0)
            {
                comboBoxSubtitleFont.SelectedIndex = 0;
            }

            if (Configuration.Settings.Tools.GenVideoFontSize >= numericUpDownFontSize.Minimum &&
                Configuration.Settings.Tools.GenVideoFontSize >= numericUpDownFontSize.Minimum)
            {
                numericUpDownFontSize.Value = Configuration.Settings.Tools.GenVideoFontSize;
            }

            checkBoxRightToLeft.Checked = Configuration.Settings.General.RightToLeftMode && LanguageAutoDetect.CouldBeRightToLeftLanguage(_assaSubtitle);
            textBoxLog.Visible = false;

            UiUtil.FixLargeFonts(this, buttonGenerate);
            UiUtil.FixFonts(this, 2000);


            _mediaInfo = FfmpegMediaInfo.Parse(inputVideoFileName);

            if (_videoInfo != null && _videoInfo.TotalSeconds > 0)
            {
                var timeSpan = TimeSpan.FromSeconds((long)Math.Round(_videoInfo.TotalSeconds + 0.5));
                numericUpDownCutToHours.Value = timeSpan.Hours;
                numericUpDownCutToMinutes.Value = timeSpan.Minutes;
                numericUpDownCutToSeconds.Value = timeSpan.Seconds;

                if (setStartEndCut && assaSubtitle != null && assaSubtitle.Paragraphs.Count > 0 &&
                    !assaSubtitle.Paragraphs.First().StartTime.IsMaxTime &&
                    !assaSubtitle.Paragraphs.Last().EndTime.IsMaxTime)
                {
                    timeSpan = assaSubtitle.Paragraphs.First().StartTime.TimeSpan;
                    numericUpDownCutFromHours.Value = timeSpan.Hours;
                    numericUpDownCutFromMinutes.Value = timeSpan.Minutes;
                    numericUpDownCutFromSeconds.Value = timeSpan.Seconds;

                    timeSpan = assaSubtitle.Paragraphs.Last().EndTime.TimeSpan;
                    if (timeSpan.Milliseconds > 0)
                    {
                        timeSpan = timeSpan.Add(TimeSpan.FromSeconds(1));
                    }
                    numericUpDownCutToHours.Value = timeSpan.Hours;
                    numericUpDownCutToMinutes.Value = timeSpan.Minutes;
                    numericUpDownCutToSeconds.Value = timeSpan.Seconds;

                    checkBoxCut.Checked = true;
                }

                checkBoxCut_CheckedChanged(null, null);
            }
            else
            {
                groupBoxCut.Visible = false;
            }

            _mpvOn = LibMpvDynamic.IsInstalled && Configuration.Settings.General.VideoPlayer == "MPV";

            if (_videoInfo != null)
            {
                _mpvSubtitleFileName = GetAssaFileName(_inputVideoFileName);
            }

            if (_mpvOn)
            {
                buttonPreview.Visible = false;
                videoPlayerContainer1.TryLoadGfx();
                videoPlayerContainer1.HidePlayerName();
            }
            else
            {
                videoPlayerContainer1.Visible = false;
            }

            if (_noSubtitles)
            {
                checkBoxRightToLeft.Enabled = false;
                labelProgress.Text = LanguageSettings.Current.Main.NoSubtitleLoaded;
                labelProgress.ForeColor = UiUtil.WarningColor;
            }

            listViewBatch.Visible = BatchMode;
            buttonRemoveFile.Visible = BatchMode;
            buttonClear.Visible = BatchMode;
            buttonAddFile.Visible = BatchMode;

            var audioTracks = _mediaInfo.Tracks.Where(p => p.TrackType == FfmpegTrackType.Audio).ToList();
            if (BatchMode)
            {
                listViewAudioTracks.Visible = false;
            }
            else if (audioTracks.Count > 1)
            {
                listViewAudioTracks.Visible = true;
                for (var index = 0; index < audioTracks.Count; index++)
                {
                    var trackInfo = audioTracks[index];
                    listViewAudioTracks.Items.Add($"#{index}: {trackInfo.TrackInfo}");
                    listViewAudioTracks.Items[index].Checked = true;
                }

                listViewAudioTracks.AutoSizeLastColumn();
            }
            else
            {
                listViewAudioTracks.Visible = false;
            }


            if (string.IsNullOrEmpty(inputVideoFileName) || _videoInfo == null || _videoInfo.Width == 0 || _videoInfo.Height == 0)
            {
                buttonMode_Click(null, null);
                buttonMode.Visible = false;
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _abort = true;
            if (buttonGenerate.Enabled)
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (string.IsNullOrWhiteSpace(outLine.Data))
            {
                return;
            }

            _log?.AppendLine(outLine.Data);

            var match = FrameFinderRegex.Match(outLine.Data);
            if (!match.Success)
            {
                return;
            }

            var arr = match.Value.Split('=');
            if (arr.Length != 2)
            {
                return;
            }

            if (long.TryParse(arr[1].Trim(), out var f))
            {
                _processedFrames = f;
            }
        }

        private void buttonGenerate_Click(object sender, EventArgs e)
        {
            if (BatchMode && _batchVideoAndSubList.Count == 0)
            {
                MessageBox.Show("No video files added for batch.");
                return;
            }

            labelProgress.Text = string.Empty;
            labelProgress.ForeColor = UiUtil.ForeColor;

            if (checkBoxCut.Checked)
            {
                var cutStart = GetCutStart();
                var cutEnd = GetCutEnd();

                if (cutStart >= cutEnd)
                {
                    MessageBox.Show("Cut end time must be after cut start time");
                    return;
                }

                if (cutStart.TotalSeconds >= _videoInfo.TotalSeconds)
                {
                    MessageBox.Show("cut start time must not be after video end time");
                    return;
                }
            }

            _log = new StringBuilder();
            buttonGenerate.Enabled = false;
            var oldFontSizeEnabled = numericUpDownFontSize.Enabled;
            numericUpDownFontSize.Enabled = false;

            Stopwatch stopWatch;
            if (BatchMode)
            {
                stopWatch = Stopwatch.StartNew();
                var sbInfo = new StringBuilder();
                sbInfo.AppendLine($"Input for convert: {_batchVideoAndSubList.Count} video files:");
                sbInfo.AppendLine();
                var okCount = 0;
                var failCount = 0;
                for (var index = 0; index < _batchVideoAndSubList.Count; index++)
                {
                    var videoAndSub = _batchVideoAndSubList[index];
                    _videoInfo = UiUtil.GetVideoInfo(videoAndSub.VideoFileName);
                    var subtitle = new Subtitle();
                    var path = Path.GetDirectoryName(videoAndSub.VideoFileName);
                    var nameNoExt = Path.GetFileNameWithoutExtension(videoAndSub.VideoFileName);
                    var ext = Path.GetExtension(videoAndSub.VideoFileName);
                    VideoFileName = Path.Combine(path, nameNoExt.TrimEnd('.', '.') + "_new" + ext);

                    if (ConvertVideo(oldFontSizeEnabled, videoAndSub.VideoFileName, subtitle))
                    {
                        listViewBatch.Items[index].SubItems[3].Text = "Converted";
                        sbInfo.AppendLine($"{index + 1}: {videoAndSub.VideoFileName} -> {VideoFileName}");
                        okCount++;
                    }
                    else
                    {
                        listViewBatch.Items[index].SubItems[3].Text = "Error";
                        sbInfo.AppendLine($"{index + 1}: {videoAndSub.VideoFileName} -> Failed!");
                        failCount++;
                    }
                }

                sbInfo.AppendLine();
                sbInfo.AppendLine($"Video files converted in {stopWatch.Elapsed}: {okCount}");
                if (failCount > 0)
                {
                    sbInfo.AppendLine($"Video files failed: {failCount}");
                }

                BatchInfo = sbInfo.ToString();
            }
            else
            {
                using (var saveDialog = new SaveFileDialog
                {
                    FileName = SuggestNewVideoFileName(),
                    Filter = "MP4|*.mp4|Matroska|*.mkv|WebM|*.webm",
                    AddExtension = true,
                    InitialDirectory = Path.GetDirectoryName(_inputVideoFileName),
                })
                {
                    if (comboBoxVideoEncoding.Text == "prores_ks")
                    {
                        saveDialog.Filter = "mov|*.mov|Matroska|*.mkv|Material eXchange Format|*.mxf";
                    }

                    if (saveDialog.ShowDialog(this) != DialogResult.OK)
                    {
                        buttonGenerate.Enabled = true;
                        numericUpDownFontSize.Enabled = true;
                        return;
                    }

                    VideoFileName = saveDialog.FileName;
                }

                stopWatch = Stopwatch.StartNew();
                if (!ConvertVideo(oldFontSizeEnabled, _inputVideoFileName, _assaSubtitle))
                {
                    return;
                }
            }


            progressBar1.Visible = false;
            labelPleaseWait.Visible = false;
            timer1.Stop();
            MillisecondsEncoding = stopWatch.ElapsedMilliseconds;
            labelProgress.Text = string.Empty;
            groupBoxSettings.Enabled = true;

            _converting = false;
            if (_abort)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }

            if (!BatchMode && (!File.Exists(VideoFileName) || new FileInfo(VideoFileName).Length == 0))
            {
                SeLogger.Error(Environment.NewLine + "Generate hard subbed video failed: " + Environment.NewLine + _log);
                MessageBox.Show("Generate embedded video failed" + Environment.NewLine +
                                "For more info see the error log: " + SeLogger.ErrorFile);
                buttonGenerate.Enabled = true;
                numericUpDownFontSize.Enabled = oldFontSizeEnabled;
                return;
            }

            DialogResult = DialogResult.OK;
        }

        private bool ConvertVideo(bool oldFontSizeEnabled, string videoFileName, Subtitle subtitle)
        {
            if (File.Exists(VideoFileName))
            {
                try
                {
                    File.Delete(VideoFileName);
                }
                catch
                {
                    MessageBox.Show($"Cannot overwrite video file {VideoFileName} - probably in use!");
                    buttonGenerate.Enabled = true;
                    numericUpDownFontSize.Enabled = oldFontSizeEnabled;
                    return false;
                }
            }

            _converting = true;
            _totalFrames = (long)_videoInfo.TotalFrames;

            _log = new StringBuilder();
            _log.AppendLine("Target file name: " + videoFileName);
            _log.AppendLine("Video info width: " + _videoInfo.Width);
            _log.AppendLine("Video info width: " + _videoInfo.Height);
            _log.AppendLine("Video info total frames: " + _videoInfo.TotalFrames);
            _log.AppendLine("Video info total seconds: " + _videoInfo.TotalSeconds);

            labelFileName.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TargetFileName, VideoFileName);
            if (!_isAssa)
            {
                SetStyleForNonAssa(subtitle);
            }

            FixRightToLeft(subtitle);

            var format = new AdvancedSubStationAlpha();
            var assaTempFileName = GetAssaFileName(videoFileName);

            if (checkBoxCut.Checked)
            {
                var cutStart = GetCutStart();
                if (cutStart.TotalMilliseconds > 0.001)
                {
                    var paragraphs = new List<Paragraph>();
                    subtitle.AddTimeToAllParagraphs(-cutStart);
                    foreach (var assaP in subtitle.Paragraphs)
                    {
                        if (assaP.StartTime.TotalMilliseconds > 0 && assaP.EndTime.TotalMilliseconds > 0)
                        {
                            paragraphs.Add(assaP);
                        }
                        else if (assaP.EndTime.TotalMilliseconds > 0)
                        {
                            assaP.StartTime.TotalMilliseconds = 0;
                            paragraphs.Add(assaP);
                        }
                    }

                    subtitle.Paragraphs.Clear();
                    subtitle.Paragraphs.AddRange(paragraphs);
                }
            }

            if (Configuration.Settings.General.CurrentVideoIsSmpte && (decimal)_videoInfo.FramesPerSecond % 1 != 0)
            {
                foreach (var assaP in subtitle.Paragraphs)
                {
                    assaP.StartTime.TotalMilliseconds *= 1.001;
                    assaP.EndTime.TotalMilliseconds *= 1.001;
                }
            }

            FileUtil.WriteAllText(assaTempFileName, format.ToText(subtitle, null), new TextEncoding(Encoding.UTF8, "UTF8"));

            groupBoxSettings.Enabled = false;
            labelPleaseWait.Visible = true;
            if (_videoInfo.TotalFrames > 0)
            {
                progressBar1.Visible = true;
            }

            if (checkBoxCut.Checked)
            {
                var cutLength = GetCutEnd() - GetCutStart();
                var factor = _videoInfo.TotalMilliseconds / cutLength.TotalMilliseconds;
                _totalFrames = (long)Math.Round(_totalFrames / factor) + 10;
            }

            if (checkBoxTargetFileSize.Checked)
            {
                RunTwoPassEncoding(assaTempFileName, videoFileName);
            }
            else
            {
                RunOnePassEncoding(assaTempFileName, videoFileName);
            }

            try
            {
                File.Delete(assaTempFileName);
            }
            catch
            {
                // ignore
            }

            return true;
        }

        private TimeSpan GetCutEnd()
        {
            return TimeSpan.FromSeconds(
                (double)numericUpDownCutToHours.Value * 60 * 60 +
                (double)numericUpDownCutToMinutes.Value * 60 +
                (double)numericUpDownCutToSeconds.Value);
        }

        private TimeSpan GetCutStart()
        {
            return TimeSpan.FromSeconds(
                (double)numericUpDownCutFromHours.Value * 60 * 60 +
                (double)numericUpDownCutFromMinutes.Value * 60 +
                (double)numericUpDownCutFromSeconds.Value);
        }

        private static string GetAssaFileName(string inputVideoFileName)
        {
            var path = Path.GetDirectoryName(inputVideoFileName);
            for (var i = 0; i < int.MaxValue; i++)
            {
                var guidLetters = Guid.NewGuid().ToString().RemoveChar('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-');
                var fileName = Path.Combine(path, $"{guidLetters}.ass");
                if (!File.Exists(fileName))
                {
                    return fileName;
                }
            }

            return Path.Combine(path, "qwerty12.ass");
        }

        private string SuggestNewVideoFileName()
        {
            var fileName = Path.GetFileNameWithoutExtension(_inputVideoFileName);

            fileName += ".burn-in";

            fileName += $".{numericUpDownWidth.Value}x{numericUpDownHeight.Value}";

            if (comboBoxVideoEncoding.Text == "libx265" || comboBoxVideoEncoding.Text == "hevc_nvenc" || comboBoxVideoEncoding.Text == "hevc_amf")
            {
                fileName += ".x265";
            }
            else if (comboBoxVideoEncoding.Text == "libvpx-vp9")
            {
                fileName += ".vp9";
            }
            else if (comboBoxVideoEncoding.Text == "prores_ks")
            {
                fileName += ".ProRes";
            }
            else
            {
                fileName += ".x264";
            }

            if (checkBoxCut.Enabled && checkBoxCut.Checked)
            {
                fileName += $".{numericUpDownCutFromHours.Value}-{numericUpDownCutFromMinutes.Value}-{numericUpDownCutFromSeconds.Value}_{numericUpDownCutToHours.Value}-{numericUpDownCutToMinutes.Value}-{numericUpDownCutToSeconds.Value}";
            }

            if (comboBoxVideoEncoding.Text == "prores_ks")
            {
                return fileName.Replace(".", "_") + ".mov";
            }

            return fileName.Replace(".", "_") + ".mp4";
        }

        private void FixRightToLeft(Subtitle subtitle)
        {
            if (!checkBoxRightToLeft.Checked)
            {
                return;
            }

            for (var index = 0; index < subtitle.Paragraphs.Count; index++)
            {
                var paragraph = subtitle.Paragraphs[index];
                if (LanguageAutoDetect.ContainsRightToLeftLetter(paragraph.Text))
                {
                    paragraph.Text = Utilities.FixRtlViaUnicodeChars(paragraph.Text);
                }
            }
        }

        private void RunTwoPassEncoding(string assaTempFileName, string videoFileName)
        {
            labelPass.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.PassX, "1");

            var bitRate = GetVideoBitRate();
            var videoBitRate = bitRate.ToString(CultureInfo.InvariantCulture) + "k";

            if (bitRate < 10)
            {
                MessageBox.Show($"Bitrate too low: {bitRate}k");
                return;
            }

            var process = GetFfmpegProcess(videoFileName, VideoFileName, assaTempFileName, 1, videoBitRate);
            _log.AppendLine("ffmpeg arguments pass 1: " + process.StartInfo.Arguments);
            if (!CheckForPromptParameters(process, Text + " - Pass 1"))
            {
                _abort = true;
                return;
            }

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _startTicks = DateTime.UtcNow.Ticks;
            timer1.Start();

            while (!process.HasExited)
            {
                System.Threading.Thread.Sleep(100);
                WindowsHelper.PreventStandBy();
                Application.DoEvents();
                if (_abort)
                {
                    process.Kill();
                    return;
                }

                var v = (int)_processedFrames;
                SetProgress(v);
            }


            labelPass.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.PassX, "2");
            process = GetFfmpegProcess(_inputVideoFileName, VideoFileName, assaTempFileName, 2, videoBitRate);
            _log.AppendLine("ffmpeg arguments pass 2: " + process.StartInfo.Arguments);
            if (!CheckForPromptParameters(process, Text + " - Pass 2"))
            {
                _abort = true;
                return;
            }
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _startTicks = DateTime.UtcNow.Ticks;
            timer1.Start();

            while (!process.HasExited)
            {
                System.Threading.Thread.Sleep(100);
                WindowsHelper.PreventStandBy();
                Application.DoEvents();
                if (_abort)
                {
                    process.Kill();
                    return;
                }

                var v = (int)_processedFrames;
                SetProgress(v);
            }
        }

        private void SetProgress(int v)
        {
            if (_totalFrames == 0)
            {
                progressBar1.Value = progressBar1.Maximum;
            }

            var pct = Math.Min(progressBar1.Maximum, (int)Math.Round(v * 100.0 / _totalFrames, MidpointRounding.AwayFromZero));
            if (pct >= progressBar1.Minimum && pct <= progressBar1.Maximum && _totalFrames > 0)
            {
                progressBar1.Value = pct;
            }
        }

        private int GetAudioFileSizeInMb()
        {
            var ffmpegLocation = Configuration.Settings.General.FFmpegLocation;
            if (!Configuration.IsRunningOnWindows && (string.IsNullOrEmpty(ffmpegLocation) || !File.Exists(ffmpegLocation)))
            {
                ffmpegLocation = "ffmpeg";
            }

            var tempFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".aac");
            var process = new Process
            {
                StartInfo =
                {
                    FileName = ffmpegLocation,
                    Arguments = $"-i \"{_inputVideoFileName}\" -vn -acodec copy \"{tempFileName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            process.WaitForExit();
            try
            {
                var length = (int)Math.Round(new FileInfo(tempFileName).Length / 1024.0 / 1024);
                try
                {
                    File.Delete(tempFileName);
                }
                catch
                {
                    // ignore
                }

                return length;
            }
            catch
            {
                return 0;
            }
        }

        private void RunOnePassEncoding(string assaTempFileName, string videoFileName)
        {
            var process = GetFfmpegProcess(videoFileName, VideoFileName, assaTempFileName);
            _log.AppendLine("ffmpeg arguments: " + process.StartInfo.Arguments);

            if (!CheckForPromptParameters(process, Text))
            {
                _abort = true;
                return;
            }

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _startTicks = DateTime.UtcNow.Ticks;
            timer1.Start();

            while (!process.HasExited)
            {
                Application.DoEvents();
                WindowsHelper.PreventStandBy();
                System.Threading.Thread.Sleep(100);
                if (_abort)
                {
                    process.Kill();
                }

                var v = (int)_processedFrames;
                SetProgress(v);
            }
        }

        private bool CheckForPromptParameters(Process process, string title)
        {
            if (!_promptFFmpegParameters)
            {
                return true;
            }

            using (var form = new GenerateVideoFFmpegPrompt(title, process.StartInfo.Arguments))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }

                _log.AppendLine("ffmpeg arguments custom: " + process.StartInfo.Arguments);
                process.StartInfo.Arguments = form.Parameters;
            }

            return true;
        }

        private Process GetFfmpegProcess(string inputVideoFileName, string outputVideoFileName, string assaTempFileName, int? passNumber = null, string twoPassBitRate = null, bool preview = false)
        {
            var audioCutTracks = string.Empty;
            if (listViewAudioTracks.Visible)
            {
                for (var index = 0; index < listViewAudioTracks.Items.Count; index++)
                {
                    var listViewItem = listViewAudioTracks.Items[index];
                    if (!listViewItem.Checked)
                    {
                        audioCutTracks += $"-map 0:a:{index} ";
                    }
                }
            }

            var pass = string.Empty;
            if (passNumber.HasValue)
            {
                pass = passNumber.Value.ToString(CultureInfo.InvariantCulture);
            }

            var cutStart = string.Empty;
            var cutEnd = string.Empty;
            if (checkBoxCut.Checked && !preview)
            {
                var start = GetCutStart();
                cutStart = $"-ss {start.Hours:00}:{start.Minutes:00}:{start.Seconds:00}";

                var end = GetCutEnd();
                var duration = end - start;
                cutEnd = $"-t {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
            }

            return VideoPreviewGenerator.GenerateHardcodedVideoFile(
                Path.GetFileName(inputVideoFileName),
                assaTempFileName,
                outputVideoFileName,
                (int)numericUpDownWidth.Value,
                (int)numericUpDownHeight.Value,
                comboBoxVideoEncoding.Text,
                comboBoxPreset.Text,
                comboBoxCrf.Text,
                comboBoxAudioEnc.Text,
                checkBoxMakeStereo.Checked,
                comboBoxAudioSampleRate.Text.Replace("Hz", string.Empty).Trim(),
                comboBoxTune.Text,
                comboBoxAudioBitRate.Text,
                pass,
                twoPassBitRate,
                OutputHandler,
                cutStart,
                cutEnd,
                audioCutTracks);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_processedFrames <= 0 || _videoInfo.TotalFrames <= 0)
            {
                return;
            }

            var durationMs = (DateTime.UtcNow.Ticks - _startTicks) / 10_000;
            var msPerFrame = (float)durationMs / _processedFrames;
            var estimatedTotalMs = msPerFrame * _totalFrames;
            var estimatedLeft = ProgressHelper.ToProgressTime(estimatedTotalMs - durationMs);
            labelProgress.Text = estimatedLeft;
        }

        private void numericUpDownWidth_ValueChanged(object sender, EventArgs e)
        {
            var v = (int)numericUpDownWidth.Value;
            if (v % 2 == 1)
            {
                numericUpDownWidth.Value++;
            }

            UpdateVideoPreview();
        }

        private void numericUpDownHeight_ValueChanged(object sender, EventArgs e)
        {
            var v = (int)numericUpDownHeight.Value;
            if (v % 2 == 1)
            {
                numericUpDownHeight.Value++;
            }

            UpdateVideoPreview();
        }

        private void comboBoxAudioEnc_SelectedIndexChanged(object sender, EventArgs e)
        {
            checkBoxMakeStereo.Enabled = comboBoxAudioEnc.Text != "copy";
            comboBoxAudioSampleRate.Enabled = comboBoxAudioEnc.Text != "copy";
            comboBoxAudioBitRate.Enabled = comboBoxAudioEnc.Text != "copy";

            numericUpDownTargetFileSize_ValueChanged(null, null);
        }

        private void GenerateVideoWithHardSubs_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                if (textBoxLog.Visible)
                {
                    textBoxLog.Visible = false;
                }
                else
                {
                    textBoxLog.Visible = true;
                    textBoxLog.BringToFront();
                    textBoxLog.Dock = DockStyle.Fill;

                    if (_log == null)
                    {
                        var log = new StringBuilder();
                        log.AppendLine("Video info width: " + _videoInfo.Width);
                        log.AppendLine("Video info width: " + _videoInfo.Height);
                        log.AppendLine("Video info total frames: " + _videoInfo.TotalFrames);
                        log.AppendLine("Video info total seconds: " + _videoInfo.TotalSeconds);
                        log.AppendLine();
                        log.AppendLine("ffmpeg " + GetFfmpegProcess(_inputVideoFileName, "output.mp4", "input.ass", null).StartInfo.Arguments);
                        textBoxLog.Text = log.ToString();
                    }
                    else
                    {
                        textBoxLog.Text = _log.ToString();
                    }

                    textBoxLog.Focus();
                    textBoxLog.SelectionStart = textBoxLog.Text.Length;
                    textBoxLog.ScrollToCaret();
                }
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && _log == null)
            {
                DialogResult = DialogResult.Cancel;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && _log == null)
            {
                DialogResult = DialogResult.Cancel;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyData == UiUtil.HelpKeys)
            {
                linkLabelHelp_LinkClicked(null, null);
            }
        }

        private void linkLabelHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (comboBoxVideoEncoding.Text == "libx265")
            {
                UiUtil.OpenUrl("http://trac.ffmpeg.org/wiki/Encode/H.265");
            }
            else if (comboBoxVideoEncoding.Text == "libvpx-vp9")
            {
                UiUtil.OpenUrl("http://trac.ffmpeg.org/wiki/Encode/VP9");
            }
            else if (comboBoxVideoEncoding.Text == "h264_nvenc" || comboBoxVideoEncoding.Text == "hevc_nvenc")
            {
                UiUtil.OpenUrl("https://trac.ffmpeg.org/wiki/HWAccelIntro");
            }
            else if (comboBoxVideoEncoding.Text == "prores_ks")
            {
                UiUtil.OpenUrl("https://ottverse.com/ffmpeg-convert-to-apple-prores-422-4444-hq");
            }
            else
            {
                UiUtil.OpenUrl("http://trac.ffmpeg.org/wiki/Encode/H.264");
            }
        }

        private void GenerateVideoWithHardSubs_FormClosing(object sender, FormClosingEventArgs e)
        {
            Configuration.Settings.Tools.GenVideoFontName = comboBoxSubtitleFont.Text;
            Configuration.Settings.Tools.GenVideoFontSize = (int)numericUpDownFontSize.Value;
            Configuration.Settings.Tools.GenVideoEncoding = comboBoxVideoEncoding.Text;
            Configuration.Settings.Tools.GenVideoPreset = comboBoxPreset.Text;
            Configuration.Settings.Tools.GenVideoCrf = comboBoxCrf.Text;
            Configuration.Settings.Tools.GenVideoTune = comboBoxTune.Text;
            Configuration.Settings.Tools.GenVideoAudioEncoding = comboBoxAudioEnc.Text;
            Configuration.Settings.Tools.GenVideoAudioForceStereo = checkBoxMakeStereo.Checked;
            Configuration.Settings.Tools.GenVideoAudioSampleRate = comboBoxAudioSampleRate.Text;
            Configuration.Settings.Tools.GenVideoTargetFileSize = checkBoxTargetFileSize.Checked;
            Configuration.Settings.Tools.GenVideoNonAssaBox = checkBoxBox.Checked;
            Configuration.Settings.Tools.GenVideoNonAssaAlignRight = checkBoxAlignRight.Checked;
            Configuration.Settings.Tools.GenVideoNonAssaFixRtlUnicode = checkBoxRightToLeft.Checked;
            Configuration.Settings.Tools.GenVideoNonAssaBoxColor = panelOutlineColor.BackColor;
            Configuration.Settings.Tools.GenVideoNonAssaTextColor = panelForeColor.BackColor;

            CloseVideo();

            if (_videoInfo != null)
            {
                using (var graphics = CreateGraphics())
                {
                    using (var font = new Font(UiUtil.GetDefaultFont().FontFamily, (float)numericUpDownFontSize.Value, FontStyle.Regular))
                    {
                        var currentHeight = graphics.MeasureString("HJKLj", font).Height;
                        Configuration.Settings.Tools.GenVideoFontSizePercentOfHeight = (float)(currentHeight / _videoInfo.Height);
                    }
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(_mpvSubtitleFileName))
                {
                    File.Delete(_mpvSubtitleFileName);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void comboBoxVideoEncoding_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxCrf.Items.Clear();
            comboBoxCrf.BeginUpdate();
            comboBoxCrf.Items.Add(string.Empty);
            labelTune.Visible = true;
            comboBoxTune.Visible = true;
            labelCRF.Visible = true;
            comboBoxCrf.Visible = true;
            labelPreset.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Preset;
            labelCRF.Text = LanguageSettings.Current.GenerateVideoWithBurnedInSubs.Crf;
            labelCrfHint.Text = string.Empty;

            FillPresets(comboBoxVideoEncoding.Text);
            FillTuneIn(comboBoxVideoEncoding.Text);

            if (comboBoxVideoEncoding.Text == "libx265")
            {
                for (var i = 0; i < 51; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                comboBoxCrf.Text = "28";
            }
            else if (comboBoxVideoEncoding.Text == "libvpx-vp9")
            {
                for (var i = 4; i <= 63; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                comboBoxCrf.Text = "10";
            }
            else if (comboBoxVideoEncoding.Text == "h264_nvenc" ||
                     comboBoxVideoEncoding.Text == "hevc_nvenc")
            {
                comboBoxCrf.Items.Clear();
                comboBoxCrf.Items.Add(string.Empty);
                for (var i = 0; i <= 51; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                labelCRF.Text = "CQ";
                labelCrfHint.Text = "0=best quality, 51=best speed";
                comboBoxCrf.Text = string.Empty;
            }
            else if (comboBoxVideoEncoding.Text == "h264_amf" ||
                     comboBoxVideoEncoding.Text == "hevc_amf")
            {
                comboBoxCrf.Items.Clear();
                comboBoxCrf.Items.Add(string.Empty);
                for (var i = 0; i <= 10; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                labelCRF.Text = "Quality";
                labelCrfHint.Text = "0=best quality, 10=best speed";
                comboBoxCrf.Text = string.Empty;
            }
            else if (comboBoxVideoEncoding.Text == "prores_ks")
            {
                labelPreset.Text = "Profile";
                comboBoxPreset.SelectedItem = 2;

                // https://ottverse.com/ffmpeg-convert-to-apple-prores-422-4444-hq/

                labelCRF.Visible = false;
                comboBoxCrf.Visible = false;

                labelTune.Visible = false;
                comboBoxTune.Visible = false;
            }
            else
            {
                for (var i = 17; i <= 28; i++)
                {
                    comboBoxCrf.Items.Add(i.ToString(CultureInfo.InvariantCulture));
                }

                comboBoxCrf.Text = "23";
            }
            comboBoxCrf.EndUpdate();
        }

        private void FillTuneIn(string videoCodec)
        {
            var items = new List<string>
            {
                string.Empty,
                "film",
                "animation",
                "grain",
            };

            string defaultItem = "";
            if (_loading)
            {
                defaultItem = Configuration.Settings.Tools.GenVideoTune;
            }

            if (comboBoxVideoEncoding.Text == "libx265")
            {
                items = new List<string>
                {
                    string.Empty,
                    "psnr",
                    "ssim",
                    "grain",
                    "zerolatency",
                    "fastdecode",
                };
            }
            else if (videoCodec == "libx264")
            {
                items = new List<string>
                {
                    string.Empty,
                    "film",
                    "animation",
                    "grain",
                    "stillimage",
                    "fastdecode",
                    "zerolatency",
                };
            }
            else if (videoCodec == "h264_nvenc")
            {
                items = new List<string>
                {
                    string.Empty,
                    "hq",
                    "ll",
                    "ull",
                    "lossless",
                };
            }
            else if (videoCodec == "hevc_nvenc")
            {
                items = new List<string>
                {
                    string.Empty,
                    "hq",
                    "ll",
                    "ull",
                    "lossless",
                };
            }
            else if (videoCodec == "h264_amf")
            {
                items = new List<string>
                {
                    string.Empty,
                };
            }
            else if (videoCodec == "hevc_amf")
            {
                items = new List<string> { string.Empty };
            }
            else if (videoCodec == "libvpx-vp9")
            {
                items = new List<string> { string.Empty };
                labelTune.Visible = false;
                comboBoxTune.Visible = false;
                comboBoxTune.Text = string.Empty;
            }
            else if (videoCodec == "prores_ks")
            {
                items = new List<string> { string.Empty };
                labelTune.Visible = false;
                comboBoxTune.Visible = false;
                comboBoxTune.Text = string.Empty;
            }

            comboBoxTune.Items.Clear();
            foreach (var item in items)
            {
                comboBoxTune.Items.Add(item);
            }

            comboBoxTune.Text = items.Contains(defaultItem) ? defaultItem : string.Empty;
        }

        private void FillPresets(string videoCodec)
        {
            var items = new List<string>
            {
               "ultrafast",
               "superfast",
               "veryfast",
               "faster",
               "fast",
               "medium",
               "slow",
               "slower",
               "veryslow",
            };

            string defaultItem = "medium";
            if (_loading)
            {
                defaultItem = Configuration.Settings.Tools.GenVideoPreset;
            }

            if (videoCodec == "h264_nvenc")
            {
                items = new List<string>
                {
                    "default",
                    "slow",
                    "medium",
                    "fast",
                    "hp",
                    "hq",
                    "bd",
                    "ll",
                    "llhq",
                    "llhp",
                    "lossless",
                    "losslesshp",
                    "p1",
                    "p2",
                    "p3",
                    "p4",
                    "p5",
                    "p6",
                    "p7",
                };
            }
            else if (videoCodec == "hevc_nvenc")
            {
                items = new List<string>
                {
                    "default",
                    "slow",
                    "medium",
                    "fast",
                    "hp",
                    "hq",
                    "bd",
                    "ll",
                    "llhq",
                    "llhp",
                    "lossless",
                    "losslesshp",
                    "p1",
                    "p2",
                    "p3",
                    "p4",
                    "p5",
                    "p6",
                    "p7",
                };
            }
            else if (videoCodec == "h264_amf")
            {
                items = new List<string> { string.Empty };
            }
            else if (videoCodec == "hevc_amf")
            {
                items = new List<string> { string.Empty };
            }
            else if (videoCodec == "libvpx-vp9")
            {
                items = new List<string> { string.Empty };
            }
            else if (videoCodec == "prores_ks")
            {
                items = new List<string>
                {
                    "proxy",
                    "lt",
                    "standard",
                    "hq",
                    "4444",
                    "4444xq",
                };
            }

            comboBoxPreset.Items.Clear();
            foreach (var item in items)
            {
                comboBoxPreset.Items.Add(item);
                if (item == defaultItem)
                {
                    comboBoxPreset.SelectedIndex = comboBoxPreset.Items.Count - 1;
                    comboBoxPreset.Text = defaultItem;
                }
            }

            if (comboBoxPreset.SelectedIndex < 0 && comboBoxPreset.Items.Count > 0)
            {
                comboBoxPreset.SelectedIndex = 0;
                comboBoxPreset.Text = string.Empty;
            }
        }

        private void GenerateVideoWithHardSubs_Shown(object sender, EventArgs e)
        {
            panelOutlineColor.BackColor = Configuration.Settings.Tools.GenVideoNonAssaBoxColor;
            panelForeColor.BackColor = Configuration.Settings.Tools.GenVideoNonAssaTextColor;

            if (_videoInfo == null)
            {
                _loading = false;
                return;
            }

            var targetFileSizeMb = (int)Math.Round(new FileInfo(_inputVideoFileName).Length / 1024.0 / 1024);
            numericUpDownTargetFileSize.Value = Math.Max(targetFileSizeMb, numericUpDownTargetFileSize.Minimum);
            UiUtil.FixFonts(groupBoxSettings, 2000);
            _loading = false;

            if (_mpvOn)
            {
                SavePreviewSubtitle();
                UiUtil.InitializeVideoPlayerAndContainer(_inputVideoFileName, _videoInfo, videoPlayerContainer1, VideoStartLoaded, VideoStartEnded);
            }

            buttonGenerate.Focus();
        }

        private void VideoStartEnded(object sender, EventArgs e)
        {
            videoPlayerContainer1.Pause();
        }

        private void VideoStartLoaded(object sender, EventArgs e)
        {
            videoPlayerContainer1.Pause();
            if (videoPlayerContainer1.VideoPlayer is LibMpvDynamic libmpv)
            {
                libmpv.LoadSubtitle(_mpvSubtitleFileName);
            }

            if (_assaSubtitle?.Paragraphs.Count > 0)
            {
                videoPlayerContainer1.CurrentPosition = _assaSubtitle.Paragraphs[0].StartTime.TotalSeconds + 0.1;
            }

            videoPlayerContainer1.ShowFullscreenButton = true;
            videoPlayerContainer1.OnButtonClicked += MediaPlayer_OnButtonClicked;
        }

        private void MediaPlayer_OnButtonClicked(object sender, EventArgs e)
        {
            if (sender is PictureBox pb && pb.Name == "_pictureBoxFullscreenOver")
            {
                if (_previewVideo != null && !_previewVideo.IsDisposed)
                {
                    _previewVideo.Close();
                    _previewVideo.Dispose();
                    _previewVideo = null;
                }
                else
                {
                    _previewVideo = new PreviewVideo(_inputVideoFileName, _mpvSubtitleFileName, _assaSubtitle, true);
                    _previewVideo.Show(this);
                }
            }
        }

        private void checkBoxTargetFileSize_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxTargetFileSize.Checked)
            {
                comboBoxAudioEnc.Text = "aac";
                comboBoxAudioBitRate.Enabled = true;
            }
            else
            {
                comboBoxAudioBitRate.Enabled = false;
            }

            numericUpDownTargetFileSize_ValueChanged(null, null);
        }

        private void buttonPreview_Click(object sender, EventArgs e)
        {
            labelProgress.Text = string.Empty;
            labelProgress.ForeColor = UiUtil.ForeColor;

            try
            {
                buttonPreview.Enabled = false;
                labelPreviewPleaseWait.Visible = true;

                if (LibMpvDynamic.IsInstalled && Configuration.Settings.General.VideoPlayer == "MPV")
                {
                    var temp = new Subtitle(_assaSubtitle);
                    if (!_isAssa)
                    {
                        SetStyleForNonAssa(temp);
                    }
                    FixRightToLeft(temp);
                    var subFileName = GetAssaFileName(_inputVideoFileName);
                    FileUtil.WriteAllText(subFileName, new AdvancedSubStationAlpha().ToText(temp, null), new TextEncoding(Encoding.UTF8, "UTF8"));

                    using (var form = new PreviewVideo(_inputVideoFileName, subFileName, _assaSubtitle))
                    {
                        form.ShowDialog(this);
                    }

                    return;
                }

                Cursor = Cursors.WaitCursor;

                // generate blank video
                var tempVideoFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mkv");
                var process = VideoPreviewGenerator.GenerateVideoFile(
                               tempVideoFileName,
                               2,
                               (int)numericUpDownWidth.Value,
                               (int)numericUpDownHeight.Value,
                               Color.Black,
                               true,
                               25,
                               null);
                process.Start();
                while (!process.HasExited)
                {
                    System.Threading.Thread.Sleep(100);
                    WindowsHelper.PreventStandBy();
                    Application.DoEvents();
                }

                // make temp assa file with font
                var assaTempFileName = GetAssaFileName(tempVideoFileName);
                var sub = new Subtitle();
                sub.Header = _assaSubtitle.Header;
                sub.Paragraphs.Add(new Paragraph(GetPreviewParagraph()));

                if (!_isAssa)
                {
                    SetStyleForNonAssa(sub);
                }
                FixRightToLeft(sub);
                FileUtil.WriteAllText(assaTempFileName, new AdvancedSubStationAlpha().ToText(sub, string.Empty), new TextEncoding(Encoding.UTF8, "UTF8"));

                // hardcode subtitle
                var outputVideoFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
                process = GetFfmpegProcess(tempVideoFileName, outputVideoFileName, assaTempFileName, null, null, true);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                while (!process.HasExited)
                {
                    System.Threading.Thread.Sleep(100);
                    Application.DoEvents();
                }

                string bmpFileName;
                try
                {
                    bmpFileName = VideoPreviewGenerator.GetScreenShot(outputVideoFileName, "00:00:01");
                    using (var bmp = new Bitmap(bmpFileName))
                    {
                        using (var form = new ExportPngXmlPreview(bmp))
                        {
                            form.AllowNext = false;
                            form.AllowPrevious = false;
                            labelPreviewPleaseWait.Visible = false;
                            form.ShowDialog(this);
                        }
                    }
                }
                catch
                {
                    if (comboBoxVideoEncoding.Text.EndsWith("_amf"))
                    {
                        MessageBox.Show("Unable to generate video with AMD hardware acceleration!");
                    }
                    else if (comboBoxVideoEncoding.Text.EndsWith("_nvenc"))
                    {
                        MessageBox.Show("Unable to generate video with Nvidia hardware acceleration!");
                    }
                    else
                    {
                        MessageBox.Show("Unable to generate video!");
                    }

                    Cursor = Cursors.Default;
                    buttonPreview.Enabled = true;
                    labelPreviewPleaseWait.Visible = false;
                    return;
                }


                try
                {
                    File.Delete(tempVideoFileName);
                    File.Delete(assaTempFileName);
                    File.Delete(outputVideoFileName);
                    File.Delete(bmpFileName);
                }
                catch
                {
                    // ignore
                }
            }
            finally
            {
                Cursor = Cursors.Default;
                buttonPreview.Enabled = true;
                labelPreviewPleaseWait.Visible = false;
            }
        }

        private void SetStyleForNonAssa(Subtitle sub)
        {
            sub.Header = AdvancedSubStationAlpha.DefaultHeader;
            var style = AdvancedSubStationAlpha.GetSsaStyle("Default", sub.Header);
            style.FontSize = numericUpDownFontSize.Value;
            style.FontName = comboBoxSubtitleFont.Text;
            style.Background = panelOutlineColor.BackColor;
            style.Primary = panelForeColor.BackColor;

            if (checkBoxAlignRight.Checked)
            {
                style.Alignment = "3";
            }

            if (checkBoxBox.Checked)
            {
                style.BorderStyle = "4"; // box - multi line
                style.ShadowWidth = 5;
            }

            sub.Header = AdvancedSubStationAlpha.GetHeaderAndStylesFromAdvancedSubStationAlpha(sub.Header, new List<SsaStyle> { style });
            sub.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResX", "PlayResX: " + ((int)numericUpDownWidth.Value).ToString(CultureInfo.InvariantCulture), "[Script Info]", sub.Header);
            sub.Header = AdvancedSubStationAlpha.AddTagToHeader("PlayResY", "PlayResY: " + ((int)numericUpDownHeight.Value).ToString(CultureInfo.InvariantCulture), "[Script Info]", sub.Header);
        }

        private Paragraph GetPreviewParagraph()
        {
            _assaSubtitle.Renumber();
            Paragraph longest;
            if (_assaSubtitle.Paragraphs.Count > 2)
            {
                longest = _assaSubtitle.Paragraphs.Where(p => p.Number > 1).OrderByDescending(p => p.Text.Length).FirstOrDefault();
                if (longest != null && longest.Text.Length > 2)
                {
                    return new Paragraph(longest) { StartTime = new TimeCode(0), EndTime = new TimeCode(10000) };
                }
            }

            longest = _assaSubtitle.Paragraphs.OrderByDescending(p => p.Text.Length).FirstOrDefault();
            if (longest != null && longest.Text.Length > 2)
            {
                return new Paragraph(longest) { StartTime = new TimeCode(0), EndTime = new TimeCode(10000) };
            }

            return new Paragraph("Example text", 0, 10000);
        }

        private void ResolutionPickClick(object sender, EventArgs e)
        {
            var text = (sender as ToolStripMenuItem).Text;
            var match = new Regex("\\d+x\\d+").Match(text);
            var parts = match.Value.Split('x');
            numericUpDownWidth.Value = int.Parse(parts[0]);
            numericUpDownHeight.Value = int.Parse(parts[1]);
        }

        private void buttonVideoChooseStandardRes_Click(object sender, EventArgs e)
        {
            var coordinates = buttonVideoChooseStandardRes.PointToClient(Cursor.Position);
            contextMenuStripRes.Show(buttonVideoChooseStandardRes, coordinates);
        }

        private int GetVideoBitRate()
        {
            var audioMb = 0;
            if (comboBoxAudioEnc.Text == "copy")
            {
                audioMb = GetAudioFileSizeInMb();
            }

            // (MiB * 8192 [converts MiB to kBit]) / video seconds = kBit/s total bitrate
            var bitRate = (int)Math.Round(((double)numericUpDownTargetFileSize.Value - audioMb) * 8192.0 / _videoInfo.TotalSeconds);
            if (comboBoxAudioEnc.Text != "copy" && !string.IsNullOrWhiteSpace(comboBoxAudioBitRate.Text))
            {
                var audioBitRate = int.Parse(comboBoxAudioBitRate.Text.RemoveChar('k').TrimEnd());
                bitRate -= audioBitRate;
            }

            return bitRate;
        }

        private void numericUpDownTargetFileSize_ValueChanged(object sender, EventArgs e)
        {
            labelVideoBitrate.Text = string.Empty;
            if (!checkBoxTargetFileSize.Checked)
            {
                return;
            }

            var videoBitRate = GetVideoBitRate();
            var separateAudio = comboBoxAudioEnc.Text != "copy" && !string.IsNullOrWhiteSpace(comboBoxAudioBitRate.Text);
            var audioBitRate = 0;
            if (separateAudio)
            {
                audioBitRate = int.Parse(comboBoxAudioBitRate.Text.RemoveChar('k').TrimEnd());
            }

            labelVideoBitrate.Visible = true;
            if (comboBoxAudioEnc.Text == "copy")
            {
                var audioTrack = _mediaInfo.Tracks.FirstOrDefault(p => p.TrackType == FfmpegTrackType.Audio);
                if (audioTrack?.BitRate > 0)
                {
                    audioBitRate = audioTrack.BitRate / 1024;
                }
                else
                {
                    labelVideoBitrate.Visible = false;
                }
            }

            labelVideoBitrate.Left = numericUpDownTargetFileSize.Right + 5;
            labelVideoBitrate.Text = string.Format(LanguageSettings.Current.GenerateVideoWithBurnedInSubs.TotalBitRateX, $"{(videoBitRate + audioBitRate):#,###,##0}k");
            if (separateAudio)
            {
                labelVideoBitrate.Text += $" ({videoBitRate:#,###,##0}k + {audioBitRate:#,###,##0}k)";
            }
        }

        private void comboBoxAudioBitRate_SelectedValueChanged(object sender, EventArgs e)
        {
            numericUpDownTargetFileSize_ValueChanged(null, null);
        }

        private void checkBoxCut_CheckedChanged(object sender, EventArgs e)
        {
            numericUpDownCutFromHours.Enabled = checkBoxCut.Checked;
            numericUpDownCutFromMinutes.Enabled = checkBoxCut.Checked;
            numericUpDownCutFromSeconds.Enabled = checkBoxCut.Checked;
            buttonCutFrom.Enabled = checkBoxCut.Checked;

            numericUpDownCutToHours.Enabled = checkBoxCut.Checked;
            numericUpDownCutToMinutes.Enabled = checkBoxCut.Checked;
            numericUpDownCutToSeconds.Enabled = checkBoxCut.Checked;
            buttonCutTo.Enabled = checkBoxCut.Checked;
        }

        private void buttonCutFrom_Click(object sender, EventArgs e)
        {
            var timeSpan = new TimeSpan((int)numericUpDownCutFromHours.Value, (int)numericUpDownCutFromMinutes.Value, (int)numericUpDownCutFromSeconds.Value);
            using (var form = new GetVideoPosition(_assaSubtitle, _inputVideoFileName, _videoInfo, timeSpan, LanguageSettings.Current.GenerateVideoWithBurnedInSubs.GetStartPosition))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    numericUpDownCutFromHours.Value = form.VideoPosition.Hours;
                    numericUpDownCutFromMinutes.Value = form.VideoPosition.Minutes;
                    numericUpDownCutFromSeconds.Value = form.VideoPosition.Seconds;
                }
            }
        }

        private void buttonCutTo_Click(object sender, EventArgs e)
        {
            var timeSpan = new TimeSpan((int)numericUpDownCutFromHours.Value, (int)numericUpDownCutToMinutes.Value, (int)numericUpDownCutFromSeconds.Value);
            using (var form = new GetVideoPosition(_assaSubtitle, _inputVideoFileName, _videoInfo, timeSpan, LanguageSettings.Current.GenerateVideoWithBurnedInSubs.GetStartPosition))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    numericUpDownCutToHours.Value = form.VideoPosition.Hours;
                    numericUpDownCutToMinutes.Value = form.VideoPosition.Minutes;
                    numericUpDownCutToSeconds.Value = form.VideoPosition.Seconds;
                }
            }
        }

        private void promptParameterBeforeGenerateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _promptFFmpegParameters = true;
            buttonGenerate_Click(null, null);
        }

        private void numericUpDownFontSize_ValueChanged(object sender, EventArgs e)
        {
            UpdateVideoPreview();
        }

        private void UpdateVideoPreview()
        {
            if (_loading)
            {
                return;
            }

            if (videoPlayerContainer1.VideoPlayer is LibMpvDynamic libmpv)
            {
                SavePreviewSubtitle();
                libmpv.ReloadSubtitle();
            }
        }

        private void SavePreviewSubtitle()
        {
            var temp = new Subtitle(_assaSubtitle);
            if (!_isAssa)
            {
                SetStyleForNonAssa(temp);
            }

            FixRightToLeft(temp);
            FileUtil.WriteAllText(_mpvSubtitleFileName, new AdvancedSubStationAlpha().ToText(temp, null), new TextEncoding(Encoding.UTF8, "UTF8"));
        }

        private void checkBoxBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateVideoPreview();
        }

        private void comboBoxSubtitleFont_SelectedValueChanged(object sender, EventArgs e)
        {
            UpdateVideoPreview();
        }

        private void checkBoxRightToLeft_CheckedChanged(object sender, EventArgs e)
        {
            UpdateVideoPreview();
        }

        private void checkBoxAlignRight_CheckedChanged(object sender, EventArgs e)
        {
            UpdateVideoPreview();
        }

        private void CloseVideo()
        {
            if (!_mpvOn)
            {
                return;
            }

            Application.DoEvents();
            if (videoPlayerContainer1.VideoPlayer != null)
            {
                videoPlayerContainer1.Pause();
                videoPlayerContainer1.VideoPlayer.DisposeVideoPlayer();
                videoPlayerContainer1.VideoPlayer = null;
            }
            Application.DoEvents();
        }

        private void buttonOutlineColor_Click(object sender, EventArgs e)
        {
            using (var colorChooser = new ColorChooser { Color = panelOutlineColor.BackColor })
            {
                if (colorChooser.ShowDialog() == DialogResult.OK)
                {
                    panelOutlineColor.BackColor = colorChooser.Color;
                    UpdateVideoPreview();
                }
            }
        }

        private void panelOutlineColor_MouseClick(object sender, MouseEventArgs e)
        {
            buttonOutlineColor_Click(null, null);
        }

        private void buttonForeColor_Click(object sender, EventArgs e)
        {
            using (var colorChooser = new ColorChooser { Color = panelForeColor.BackColor })
            {
                if (colorChooser.ShowDialog() == DialogResult.OK)
                {
                    panelForeColor.BackColor = colorChooser.Color;
                    UpdateVideoPreview();
                }
            }
        }

        private void panelForeColor_MouseClick(object sender, MouseEventArgs e)
        {
            buttonForeColor_Click(null, null);
        }

        private void contextMenuStripBatch_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            pickSubtitleFileToolStripMenuItem.Visible = listViewBatch.SelectedItems.Count == 1;

            if (listViewBatch.Items.Count == 0)
            {
                toolStripSeparator1.Visible = false;
                deleteToolStripMenuItem.Visible = false;
                clearToolStripMenuItem.Visible = false;
            }
            else
            {
                toolStripSeparator1.Visible = true;
                deleteToolStripMenuItem.Visible = true;
                clearToolStripMenuItem.Visible = true;
            }
        }

        private void buttonMode_Click(object sender, EventArgs e)
        {
            BatchMode = !BatchMode;
            listViewBatch.Visible = BatchMode;
            listViewBatch.AutoSizeLastColumn();
            videoPlayerContainer1.Visible = !BatchMode;
            labelInfo.Visible = !BatchMode;
            checkBoxTargetFileSize.Visible = !BatchMode;
            labelFileSize.Visible = !BatchMode;
            numericUpDownTargetFileSize.Visible = !BatchMode;
            labelVideoBitrate.Visible = !BatchMode;
            labelFileName.Visible = !BatchMode;
            buttonAddFile.Visible = BatchMode;
            buttonRemoveFile.Visible = BatchMode;
            buttonClear.Visible = BatchMode;
            buttonMode.Text = BatchMode
                ? LanguageSettings.Current.AudioToText.BatchMode
                : LanguageSettings.Current.Split.Basic;
        }

        private void addFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var openFileDialog1 = new OpenFileDialog())
            {
                openFileDialog1.Title = LanguageSettings.Current.General.OpenVideoFileTitle;
                openFileDialog1.FileName = string.Empty;
                openFileDialog1.Filter = UiUtil.GetVideoFileFilter(true);
                openFileDialog1.Multiselect = true;
                if (openFileDialog1.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                foreach (var fileName in openFileDialog1.FileNames)
                {
                    AddInputFile(fileName);
                }
            }
        }

        private void AddInputFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if ((Utilities.AudioFileExtensions.Contains(ext) || Utilities.VideoFileExtensions.Contains(ext)) && File.Exists(fileName))
            {
                var item = new BatchVideoAndSub();
                item.VideoFileName = fileName;
                item.VideoFileSizeInBytes = new FileInfo(fileName).Length;

                var path = Path.GetDirectoryName(fileName);
                var fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var subFileName = Path.ChangeExtension(fileName, ".srt");

                if (!File.Exists(subFileName))
                {
                    subFileName = Path.ChangeExtension(fileName, ".ass");
                }

                if (!File.Exists(subFileName))
                {
                    var files = Directory.GetFiles(path, fileNameNoExt + "*.srt");
                    if (files.Length > 0)
                    {
                        subFileName = files[0];
                    }
                }

                if (!File.Exists(subFileName))
                {
                    var files = Directory.GetFiles(path, fileNameNoExt + "*.ass");
                    if (files.Length > 0)
                    {
                        subFileName = files[0];
                    }
                }

                if (File.Exists(subFileName))
                {
                    item.SubtitleFileName = subFileName;
                    item.SubtitleFileFileSizeInBytes = new FileInfo(subFileName).Length;
                }

                var listViewItem = new ListViewItem(fileName);
                listViewItem.Tag = item;
                var s = Utilities.FormatBytesToDisplayFileSize(item.VideoFileSizeInBytes);
                listViewItem.SubItems.Add(s);
                listViewItem.SubItems.Add(Path.GetFileName(item.SubtitleFileName));
                listViewItem.SubItems.Add(string.Empty);
                listViewBatch.Items.Add(listViewItem);
                _batchVideoAndSubList.Add(item);
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (var i = listViewBatch.SelectedIndices.Count - 1; i >= 0; i--)
            {
                listViewBatch.Items.RemoveAt(listViewBatch.SelectedIndices[i]);
                _batchVideoAndSubList.RemoveAt(listViewBatch.SelectedIndices[i]);
            }
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listViewBatch.Items.Clear();
            _batchVideoAndSubList.Clear();
        }

        private void GenerateVideoWithHardSubs_ResizeEnd(object sender, EventArgs e)
        {
            listViewBatch.AutoSizeLastColumn();
            listViewAudioTracks.AutoSizeLastColumn();
        }

        private void listViewBatch_DragEnter(object sender, DragEventArgs e)
        {
            if (_converting)
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effect = DragDropEffects.All;
            }
        }

        private void listViewBatch_DragDrop(object sender, DragEventArgs e)
        {
            if (_converting)
            {
                return;
            }

            try
            {
                var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
                labelPleaseWait.Visible = true;

                TaskDelayHelper.RunDelayed(TimeSpan.FromMilliseconds(5), () =>
                {
                    foreach (var fileName in fileNames)
                    {
                        if (FileUtil.IsDirectory(fileName))
                        {
                            SearchFolder(fileName);
                        }
                        else
                        {
                            AddInputFile(fileName);
                        }
                    }
                });

            }
            finally
            {
                labelPleaseWait.Visible = false;
            }
        }

        private void SearchFolder(string path)
        {
            _abort = false;
            foreach (var fileName in Directory.EnumerateFiles(path))
            {
                AddInputFile(fileName);
            }
        }

        private void buttonAddFile_Click(object sender, EventArgs e)
        {
            addFilesToolStripMenuItem_Click(null, null);
        }

        private void buttonRemoveFile_Click(object sender, EventArgs e)
        {
            deleteToolStripMenuItem_Click(null, null);
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            clearToolStripMenuItem_Click(null, null);
        }
    }
}
