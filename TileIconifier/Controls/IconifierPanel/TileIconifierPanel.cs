﻿#region LICENCE

// /*
//         The MIT License (MIT)
// 
//         Copyright (c) 2016 Johnathon M
// 
//         Permission is hereby granted, free of charge, to any person obtaining a copy
//         of this software and associated documentation files (the "Software"), to deal
//         in the Software without restriction, including without limitation the rights
//         to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//         copies of the Software, and to permit persons to whom the Software is
//         furnished to do so, subject to the following conditions:
// 
//         The above copyright notice and this permission notice shall be included in
//         all copies or substantial portions of the Software.
// 
//         THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//         IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//         FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//         AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//         LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//         OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//         THE SOFTWARE.
// 
// */

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TileIconifier.Controls.PictureBox;
using TileIconifier.Core;
using TileIconifier.Core.Custom;
using TileIconifier.Core.Shortcut;
using TileIconifier.Core.Utilities;
using TileIconifier.Forms.Shared;
using TileIconifier.Properties;
using TileIconifier.Skinning.Skins;

namespace TileIconifier.Controls.IconifierPanel
{
    public partial class TileIconifierPanel : UserControl
    {
        private readonly List<PannablePictureBoxMetaData> _pannablePictureBoxMetaDatas =
            new List<PannablePictureBoxMetaData>();

        private BaseSkin _currentBaseSkin;

        public TileIconifierPanel()
        {
            InitializeComponent();
            AddEventHandlers();
        }

        public ShortcutItem CurrentShortcutItem { get; set; }

        public Size MediumPictureBoxSize => panPctMediumIcon.Size;
        public Size SmallPictureBoxSize => panPctSmallIcon.Size;

        public event EventHandler OnIconifyPanelUpdate;

        public void UpdateControlsToShortcut()
        {
            //disable event handlers whilst updating things programatically
            RemoveEventHandlers();

            //check if unsaved once per update
            var hasUnsavedChanges = CurrentShortcutItem.Properties.HasUnsavedChanges;

            //update color panel
            UpdateColorPanelControlsToCurrentShortcut();
            
            //update the picture boxes to show the relevant images
            UpdatePictureBoxImage(panPctMediumIcon, CurrentShortcutItem.Properties.CurrentState.MediumImage);
            UpdatePictureBoxImage(panPctSmallIcon, CurrentShortcutItem.Properties.CurrentState.SmallImage);
            UpdatePictureBoxOverlay(panPctMediumIcon, CurrentShortcutItem);

            UpdatePictureBoxBackColors();


            //set the associatedShortcutItemImages for each picturebox
            GetSenderPictureBoxToMetaData(panPctMediumIcon).ShortcutItemImage =
                CurrentShortcutItem.Properties.CurrentState.MediumImage;
            GetSenderPictureBoxToMetaData(panPctSmallIcon).ShortcutItemImage =
                CurrentShortcutItem.Properties.CurrentState.SmallImage;

            //update the picture box control panels
            pannablePictureBoxControlPanelMedium.UpdateControls();
            pannablePictureBoxControlPanelSmall.UpdateControls();

            //set relevant unsaved changes controls to required visibility/enabled states
            lblUnsaved.Visible = hasUnsavedChanges;
            btnReset.Enabled = hasUnsavedChanges;

            //reset any validation failures
            ResetValidation();

            //re-add the event handlers now we've finished updating
            AddEventHandlers();
        }

        private void UpdatePictureBoxBackColors()
        {
            var result = colorPanel.GetColorPanelResult();
            if (result != null)
            {
                SetPictureBoxesBackColor(result.BackgroundColor);
            }
            else
            {
                SetPictureBoxesBackColor();
            }
        }

        public void SetPictureBoxesBackColor(Color? color = null)
        {
            Action<PannablePictureBox> setBackColor = b =>
            {
                b.BackColor = b.PannablePictureBoxImage.Image == null
                    ? _currentBaseSkin.BackColor
                    : color ?? _currentBaseSkin.BackColor;
                b.Refresh();
            };
            setBackColor(panPctMediumIcon);
            setBackColor(panPctSmallIcon);
        }


        public bool DoValidation()
        {
            ResetValidation();

            return ValidateControls();
        }

        public void UpdateSkinColors(BaseSkin currentBaseSkin)
        {
            _currentBaseSkin = currentBaseSkin;
            lblUnsaved.ForeColor = _currentBaseSkin.ErrorColor;
            SetPictureBoxesBackColor();
        }

        private void UpdateColorPanelControlsToCurrentShortcut()
        {
            colorPanel.SetBackgroundColor(
                ColorUtils.HexOrNameToColor(CurrentShortcutItem.Properties.CurrentState.BackgroundColor));
            colorPanel.SetForegroundColorRadio(CurrentShortcutItem.Properties.CurrentState.ForegroundText == "light");
            colorPanel.SetForegroundTextShow(CurrentShortcutItem.Properties.CurrentState.ShowNameOnSquare150X150Logo);
        }

        private void UpdatePictureBoxOverlay(PannablePictureBox pannablePictureBox, ShortcutItem currentShortcutItem)
        {
            pannablePictureBox.ShowTextOverlay = currentShortcutItem.Properties.CurrentState.ShowNameOnSquare150X150Logo;
            pannablePictureBox.OverlayColor = currentShortcutItem.Properties.CurrentState.ForegroundText == "light"
                ? Color.White
                : Color.Black;
            pannablePictureBox.TextOverlay = Path.GetFileNameWithoutExtension(currentShortcutItem.ShortcutFileInfo.Name);
        }

        private void UpdatePictureBoxImage(PannablePictureBox pannablePictureBox, ShortcutItemImage shortcutItemImage)
        {
            pannablePictureBox.SetImage(shortcutItemImage.CachedImage(),
                shortcutItemImage.Width,
                shortcutItemImage.Height,
                shortcutItemImage.X,
                shortcutItemImage.Y);
        }

        private void RunUpdate()
        {
            UpdateControlsToShortcut();
            OnIconifyPanelUpdate?.Invoke(this, null);
        }

        private void IconSet(object sender)
        {
            IconSelectorResult selectedImage;
            try
            {
                var imagePath = GetSenderPictureBoxToMetaData(sender).ShortcutItemImage.Path;
                //if we haven't got a valid file from previously, try and get the default icon
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    //if it's a custom shortcut, try and get the target path from the VBS file
                    if (CurrentShortcutItem.IsTileIconifierCustomShortcut)
                    {
                        var customShortcutExecutionTarget = CustomShortcut.Load(CurrentShortcutItem.TargetFilePath);
                        imagePath = customShortcutExecutionTarget.TargetPath.UnQuoteWrap();
                    }
                    else
                    {
                        //otherwise we just use the target file path from the shortcut
                        imagePath = CurrentShortcutItem.TargetFilePath;
                    }
                }
                selectedImage = FrmIconSelector.GetImage(this, imagePath);
            }
            catch (UserCancellationException)
            {
                return;
            }

            var pictureBoxMetaDataToUse = chkUseSameImg.Checked
                ? _pannablePictureBoxMetaDatas
                : new List<PannablePictureBoxMetaData> {GetSenderPictureBoxToMetaData(sender)};

            foreach (var pictureBoxMetaData in pictureBoxMetaDataToUse)
            {
                pictureBoxMetaData.ShortcutItemImage.Path = selectedImage.ImagePath;
                pictureBoxMetaData.ShortcutItemImage.SetImage(selectedImage.ImageBytes, pictureBoxMetaData.Size);
                UpdatePictureBoxImage(pictureBoxMetaData.PannablePictureBox, pictureBoxMetaData.ShortcutItemImage);
                pictureBoxMetaData.PannablePictureBox.ResetImage();
            }

            RunUpdate();
        }

        private PannablePictureBoxMetaData GetSenderPictureBoxToMetaData(object sender)
        {
            PannablePictureBox senderPictureBox = null;
            if (sender.GetType() == typeof (PannablePictureBoxControlPanel))
            {
                senderPictureBox = ((PannablePictureBoxControlPanel) sender).PannablePictureBox;
            }
            if (sender.GetType() == typeof (PannablePictureBox))
            {
                senderPictureBox = (PannablePictureBox) sender;
            }
            if (senderPictureBox == null)
            {
                throw new InvalidCastException($@"Sender not valid type! Received {sender.GetType()}");
            }

            return _pannablePictureBoxMetaDatas.Single(p => p.PannablePictureBox == senderPictureBox);
        }

        private void BuildPannableShortcutBoxControlPanels()
        {
            pannablePictureBoxControlPanelMedium.SetPannablePictureBoxControl(panPctMediumIcon);
            pannablePictureBoxControlPanelSmall.SetPannablePictureBoxControl(panPctSmallIcon);
            pannablePictureBoxControlPanelMedium.ChangeImageClick += (sender, args) => { IconSet(sender); };
            pannablePictureBoxControlPanelSmall.ChangeImageClick += (sender, args) => { IconSet(sender); };
            pannablePictureBoxControlPanelMedium.UpdateTrackBarAndZoom();
            pannablePictureBoxControlPanelSmall.UpdateTrackBarAndZoom();
        }

        private void TileIconifierPanel_Load(object sender, EventArgs e)
        {
            SetupPannablePictureBoxes();
            BuildPannableShortcutBoxControlPanels();
        }

        private void SetupPannablePictureBoxes()
        {
            panPctMediumIcon.TextOverlayPoint = new Point(6, 78);

            _pannablePictureBoxMetaDatas.Add(new PannablePictureBoxMetaData
            {
                PannablePictureBox = panPctMediumIcon,
                Size = ShortcutConstantsAndEnums.MediumShortcutOutputSize
            });
            _pannablePictureBoxMetaDatas.Add(new PannablePictureBoxMetaData
            {
                PannablePictureBox = panPctSmallIcon,
                Size = ShortcutConstantsAndEnums.SmallShortcutOutputSize
            });
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            CurrentShortcutItem.Properties.UndoChanges();

            RunUpdate();
        }

        private void ResetValidation()
        {
            //TODO
            colorPanel.ResetValidation();
        }

        private bool ValidateControls()
        {
            var valid = true;

            Action<Control> controlInvalid = c =>
            {
                c.BackColor = Color.Red;
                valid = false;
            };

            if (CurrentShortcutItem.Properties.CurrentState.MediumImage.Bytes == null)
            {
                controlInvalid(panPctMediumIcon);
            }

            if (CurrentShortcutItem.Properties.CurrentState.SmallImage.Bytes == null)
            {
                controlInvalid(panPctSmallIcon);
            }

            var colorPanelValid = colorPanel.ValidateControls();

            return valid && colorPanelValid;
        }

        //TODO
        private void AddEventHandlers()
        {
            colorPanel.ColorUpdate += ColorPanelColorUpdate;
            panPctMediumIcon.OnPannablePictureImagePropertyChange +=
                PanPctMediumIcon_OnPannablePictureImagePropertyChange;
            panPctSmallIcon.OnPannablePictureImagePropertyChange += PanPctSmallIcon_OnPannablePictureImagePropertyChange;
        }

        private void ColorPanelColorUpdate(object sender, EventArgs eventArgs)
        {
            UpdateFromColorPanel((ColorPanel)sender);
            RunUpdate();
        }

        private void UpdateFromColorPanel(ColorPanel usedColorPanel)
        {
            var result = usedColorPanel.GetColorPanelResult();
            if (result == null)
            {
                return;
            }

            CurrentShortcutItem.Properties.CurrentState.BackgroundColor = ColorUtils.ColorToHex(result.BackgroundColor);
            CurrentShortcutItem.Properties.CurrentState.ShowNameOnSquare150X150Logo = result.DisplayForegroundText;
            CurrentShortcutItem.Properties.CurrentState.ForegroundText = result.ForegroundColor;
        }

        private void RemoveEventHandlers()
        {
            colorPanel.ColorUpdate -= ColorPanelColorUpdate;
            panPctMediumIcon.OnPannablePictureImagePropertyChange -=
                PanPctMediumIcon_OnPannablePictureImagePropertyChange;
            panPctSmallIcon.OnPannablePictureImagePropertyChange -= PanPctSmallIcon_OnPannablePictureImagePropertyChange;
        }

        private void panPctSmallIcon_DoubleClick(object sender, EventArgs e)
        {
            IconSet(sender);
        }

        private void panPctMediumIcon_DoubleClick(object sender, EventArgs e)
        {
            IconSet(sender);
        }

        private void panPctSmallIcon_Click(object sender, EventArgs e)
        {
            if (((MouseEventArgs) e).Button != MouseButtons.Right)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItem = new MenuItem(Strings.ChangeImage,
                (o, args) => { IconSet(panPctSmallIcon); });
            contextMenu.MenuItems.Add(menuItem);
            menuItem = new MenuItem(Strings.CentreImage,
                (o, args) => { panPctSmallIcon.CenterImage(); });
            contextMenu.MenuItems.Add(menuItem);
            contextMenu.Show(panPctSmallIcon, ((MouseEventArgs) e).Location);
        }

        private void panPctMediumIcon_Click(object sender, EventArgs e)
        {
            if (((MouseEventArgs) e).Button != MouseButtons.Right)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItem = new MenuItem(Strings.ChangeImage,
                (o, args) => { IconSet(panPctMediumIcon); });
            contextMenu.MenuItems.Add(menuItem);
            menuItem = new MenuItem(Strings.CentreImage,
                (o, args) => { panPctMediumIcon.CenterImage(); });
            contextMenu.MenuItems.Add(menuItem);
            contextMenu.Show(panPctMediumIcon, ((MouseEventArgs) e).Location);
        }


        private void PanPctMediumIcon_OnPannablePictureImagePropertyChange(object sender, EventArgs e)
        {
            var item = (PannablePictureBoxImage) sender;
            CurrentShortcutItem.Properties.CurrentState.MediumImage.X = item.X;
            CurrentShortcutItem.Properties.CurrentState.MediumImage.Y = item.Y;
            CurrentShortcutItem.Properties.CurrentState.MediumImage.Width = item.Width;
            CurrentShortcutItem.Properties.CurrentState.MediumImage.Height = item.Height;

            RunUpdate();
        }

        private void PanPctSmallIcon_OnPannablePictureImagePropertyChange(object sender, EventArgs e)
        {
            var item = (PannablePictureBoxImage) sender;
            CurrentShortcutItem.Properties.CurrentState.SmallImage.X = item.X;
            CurrentShortcutItem.Properties.CurrentState.SmallImage.Y = item.Y;
            CurrentShortcutItem.Properties.CurrentState.SmallImage.Width = item.Width;
            CurrentShortcutItem.Properties.CurrentState.SmallImage.Height = item.Height;

            RunUpdate();
        }
    }
}