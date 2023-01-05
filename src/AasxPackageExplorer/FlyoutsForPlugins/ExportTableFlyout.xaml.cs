﻿/*
Copyright (c) 2018-2021 Festo AG & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using AasxIntegrationBase;
using AdminShellNS;
using Newtonsoft.Json;

namespace AasxPackageExplorer
{
    public partial class ExportTableFlyout : UserControl, IFlyoutControl
    {
        public event IFlyoutControlAction ControlClosed;

        protected string _caption;

        public List<ImportExportTableRecord> Presets = null;

        public ImportExportTableRecord Result = null;

        public bool CloseForHelp = false;

        private int _rowsTop = 1, _rowsBody, _cols = 1;

        private List<TextBox> _textBoxesTop = new List<TextBox>();
        private List<TextBox> _textBoxesBody = new List<TextBox>();

        //
        // Init
        //

        public ExportTableFlyout(string caption = null)
        {
            InitializeComponent();
            _caption = caption;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // init with defaults
            this.ThisInit(1, 1, 4);

            // caption
            if (_caption != null)
                TextBlockCaption.Text = _caption;

            // if preset, load first preset
            if (this.Presets != null && this.Presets.Count > 0)
                this.ThisFromPreset(this.Presets[0]);

            // placeholders
            TextBoxPlaceholders.Text =
                AdminShellUtil.CleanHereStringWithNewlines(
                @"All placeholders delimited by %{..}%, {} = set arithmetics, [] = optional
                {Referable}.{idShort, category, description[@en..], elementName, elementShort, elementShort2, elementAbbreviation, kind, parent}, {Referable|Identifiable} = {SM, SME, CD}, depth, indent}
                {Identifiable}.{identification[.{idType, id}], administration.{ version, revision}}, {Qualifiable}.qualifiers, {Qualifiable}.multiplicity
                {AasCore.Aas3_0_RC02.Reference}, {AasCore.Aas3_0_RC02.Reference}[0..n], {AasCore.Aas3_0_RC02.Reference}[0..n].{type, local, idType, value}, {AasCore.Aas3_0_RC02.Reference} = {semanticId, isCaseOf, unitId}
                SME.value, Property.{value, valueType, valueId}, MultiLanguageProperty.{value, vlaueId}, Range.{valueType, min, max}, Blob.{mimeType, value}, File.{mimeType, value}, ReferenceElement.value, 
                RelationshipElement.{first, second}, SubmodelElementCollection.{value = #elements, ordered, allowDuplicates}, Entity.{entityType, asset}
                CD.{preferredName[@en..], shortName[@en..], unit, unitId, sourceOfDefinition, symbol, dataType, definition[@en..], valueFormat}
                Special: %*% = match any, %stop% = stop if non-empty, %seq={ascii}% = split sequence by char {ascii}, %opt% = optional match
                Commands for header cells include: %fg={color}%, %bg={color}% with {color} = {#a030a0, Red, blue, ..}, %halign={left, center, right}%, %valign={top, center, bottom}%,
                %font={bold, italic, underline}, %frame={0,1,2,3}% (only whole table)");

            // combo Presets
            ComboBoxPreset.Items.Clear();
            if (this.Presets != null && this.Presets.Count > 0)
            {
                foreach (var p in this.Presets)
                    ComboBoxPreset.Items.Add("" + p.Name);
                ComboBoxPreset.SelectionChanged += ComboBoxPreset_SelectionChanged;
                ComboBoxPreset.SelectedIndex = 0;
            }

            // combo Formats
            ComboBoxFormat.Items.Clear();
            foreach (var f in ImportExportTableRecord.FormatNames)
                ComboBoxFormat.Items.Add("" + f);
            ComboBoxFormat.SelectedIndex = 0;
        }

        void DataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            var dg = sender as DataGrid;
            if (dg == null)
                return;
            foreach (var oColumn in dg.Columns)
            {
                // This is how to set the width to *
                oColumn.Width = new DataGridLength(1.0, DataGridLengthUnitType.Star);
            }
        }

        //
        // Outer
        //

        public void ControlStart()
        {
        }

        public void ControlPreviewKeyDown(KeyEventArgs e)
        {
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            this.Result = null;
            ControlClosed?.Invoke();
        }

        private void ButtonHelp_Click(object sender, RoutedEventArgs e)
        {
            this.Result = null;
            this.CloseForHelp = true;
            ControlClosed?.Invoke();
        }

        //
        // Mechanics
        //

        private void ThisInit(int rowsTop, int rowsBody, int cols)
        {
            _rowsTop = rowsTop;
            _rowsBody = rowsBody;
            _cols = cols;

            TextBoxNumRowsTop.Text = "" + _rowsTop;
            TextBoxNumRowsBody.Text = "" + _rowsBody;
            TextBoxNumCols.Text = "" + _cols;

            AdaptRowsCols(GridOuterTop, _textBoxesTop, _rowsTop, _cols);
            AdaptRowsCols(GridOuterBody, _textBoxesBody, _rowsBody, _cols);
        }

        private ImportExportTableRecord ThisToPreset()
        {
            var x = new ImportExportTableRecord(_rowsTop, _rowsBody, _cols, "",
                    _textBoxesTop.Select(tb => tb.Text),
                    _textBoxesBody.Select(tb => tb.Text)
                    );

            x.Format = ComboBoxFormat.SelectedIndex;
            x.ReplaceFailedMatches = CheckBoxReplaceFailed.IsChecked == true;
            x.FailText = TextBoxFailText.Text;
            x.ActInHierarchy = CheckBoxActInHierarchy.IsChecked == true;
            if (int.TryParse(TextBoxNumRowsGap.Text, out var i))
                x.RowsGap = i;
            else
                x.RowsGap = 0;

            return x;
        }

        private void ThisFromPreset(ImportExportTableRecord preset)
        {
            // access
            if (preset == null || preset.RowsTop < 1 || preset.RowsBody < 1 || preset.Cols < 1)
                return;

            // take over
            _rowsTop = preset.RowsTop;
            _rowsBody = preset.RowsBody;
            _cols = preset.Cols;

            ComboBoxFormat.SelectedIndex = Math.Max(0, Math.Min(ComboBoxFormat.Items.Count - 1, preset.Format));

            TextBoxNumRowsTop.Text = "" + _rowsTop;
            TextBoxNumRowsBody.Text = "" + _rowsBody;
            TextBoxNumRowsBody.Text = "" + _rowsBody;
            TextBoxNumCols.Text = "" + _cols;

            CheckBoxReplaceFailed.IsChecked = preset.ReplaceFailedMatches;
            TextBoxFailText.Text = "" + preset.FailText;
            CheckBoxActInHierarchy.IsChecked = preset.ActInHierarchy;

            TextBoxNumRowsGap.Text = "" + preset.RowsGap;

            AdaptRowsCols(GridOuterTop, _textBoxesTop, _rowsTop, _cols);
            AdaptRowsCols(GridOuterBody, _textBoxesBody, _rowsBody, _cols);

            if (preset.Top != null)
                for (int i = 0; i < preset.Top.Count; i++)
                    if (i < _textBoxesTop.Count)
                        _textBoxesTop[i].Text = preset.Top[i];

            if (preset.Body != null)
                for (int i = 0; i < preset.Body.Count; i++)
                    if (i < _textBoxesBody.Count)
                        _textBoxesBody[i].Text = preset.Body[i];
        }

        private void ComboBoxPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var i = ComboBoxPreset.SelectedIndex;
            if (this.Presets != null && i >= 0 && i < this.Presets.Count)
            {
                this.ThisFromPreset(this.Presets[i]);
            }
        }

        private void ScrollViewGrids_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!(sender is ScrollViewer scrvw))
                return;

            var nc = Math.Max(1, GridOuterBody.ColumnDefinitions.Count);

            var gw = Math.Max((1 + nc) * 160, scrvw.ActualWidth - 26);

            GridInScrollViewer.MinWidth = gw;
            GridInScrollViewer.MaxWidth = gw;
            GridInScrollViewer.Width = gw;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ButtonStart)
            {
                this.Result = this.ThisToPreset();
                ControlClosed?.Invoke();
            }

            if (sender == ButtonResize)
            {
                // save the old state
                int oldRowsTop = _rowsTop, oldRowsBody = _rowsBody, oldCols = _cols;
                var oldHeader = _textBoxesTop.Select(tb => tb.Text).ToList();
                var oldElements = _textBoxesBody.Select(tb => tb.Text).ToList();

                if (!int.TryParse("" + TextBoxNumRowsTop.Text, out int irowsT)
                    || !int.TryParse("" + TextBoxNumRowsBody.Text, out int irowsB)
                    || !int.TryParse("" + TextBoxNumCols.Text, out int icols))
                    return;

                _rowsTop = Math.Max(1, irowsT);
                _rowsBody = Math.Max(1, irowsB);
                _cols = Math.Max(1, icols);

                AdaptRowsCols(GridOuterTop, _textBoxesTop, _rowsTop, _cols);
                AdaptRowsCols(GridOuterBody, _textBoxesBody, _rowsBody, _cols);

                Action<List<TextBox>, List<string>, int, int> reAssign = (tbs, oldText, rows, oldrows) =>
                {
                    for (int nr = 0; nr < 1 + rows; nr++)
                        for (int nc = 0; nc < 1 + _cols; nc++)
                        {
                            var txt = "";
                            var oldNdx = nr * (1 + oldCols) + nc;
                            if (nr < 1 + oldrows && nc < 1 + oldCols && oldNdx < oldText.Count)
                                txt = oldText[oldNdx];

                            var newNdx = nr * (1 + _cols) + nc;
                            if (newNdx < tbs.Count)
                                tbs[newNdx].Text = txt;
                        }
                };
                reAssign(_textBoxesTop, oldHeader, _rowsTop, oldRowsTop);
                reAssign(_textBoxesBody, oldElements, _rowsBody, oldRowsBody);
            }

            if (sender == ButtonSavePreset)
            {
                // choose filename
                var dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = "new.json";
                dlg.DefaultExt = "*.json";
                dlg.Filter = "Preset JSON file (*.json)|*.json|All files (*.*)|*.*";

                // save
                if (true == dlg.ShowDialog())
                {
                    try
                    {
                        var pr = this.ThisToPreset();
                        pr.SaveToFile(dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        AdminShellNS.LogInternally.That.SilentlyIgnoredError(ex);
                    }
                }
            }

            if (sender == ButtonLoadPreset)
            {
                // choose filename
                var dlg = new Microsoft.Win32.OpenFileDialog();
                dlg.FileName = "new.json";
                dlg.DefaultExt = "*.json";
                dlg.Filter = "Preset JSON file (*.json)|*.json|All files (*.*)|*.*";

                // save
                if (true == dlg.ShowDialog())
                {
                    try
                    {
                        var pr = ImportExportTableRecord.LoadFromFile(dlg.FileName);
                        this.ThisFromPreset(pr);
                    }
                    catch (Exception ex)
                    {
                        AdminShellNS.LogInternally.That.SilentlyIgnoredError(ex);
                    }
                }
            }
        }

        private void AdaptRowsCols(Grid grid, List<TextBox> tbs, int rows, int cols)
        {
            if (grid == null || tbs == null)
                return;

            // have header rows & columns
            rows += 1;
            cols += 1;

            // rework grid's cols
            while (grid.ColumnDefinitions.Count < cols)
            {
                var gl = new ColumnDefinition();
                gl.Width = new GridLength(1.0, GridUnitType.Star);
                grid.ColumnDefinitions.Add(gl);
            }
            while (grid.ColumnDefinitions.Count > cols)
            {
                grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
            }

            // rework grid's rows
            while (grid.RowDefinitions.Count < rows)
            {
                var gl = new RowDefinition();
                gl.Height = new GridLength(1.0, GridUnitType.Star);
                gl.MinHeight = 30;
                grid.RowDefinitions.Add(gl);
            }
            while (grid.RowDefinitions.Count > rows)
            {
                grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
            }

            // more than required
            while (tbs.Count > rows * cols)
            {
                // delete last from grid
                var tb = tbs[tbs.Count - 1];
                grid.Children.Remove(tb);
                tbs.Remove(tb);
            }

            // re-align the existing ones
            for (int i = 0; i < tbs.Count; i++)
            {
                var tb = tbs[i];
                Grid.SetRow(tb, i / cols);
                Grid.SetColumn(tb, i % cols);
            }

            // new text boxes
            while (tbs.Count < rows * cols)
            {
                var tb = new TextBox();
                tb.Margin = new Thickness(0, 0, 4, 4);
                tb.TextWrapping = TextWrapping.Wrap;
                tb.AcceptsReturn = true;
                grid.Children.Add(tb);
                Grid.SetRow(tb, (tbs.Count) / cols);
                Grid.SetColumn(tb, (tbs.Count) % cols);
                tbs.Add(tb);
            }

            // in any case, re-color text boxes
            foreach (var child in grid.Children)
            {
                var tb = child as TextBox;
                if (tb != null)
                    if (Grid.GetRow(tb) < 1 || Grid.GetColumn(tb) < 1)
                    {
                        tb.Foreground = Brushes.DarkGray;
                        tb.Background = new SolidColorBrush(Color.FromArgb(0xff, 0x15, 0x1f, 0x33));
                    }
                    else
                    {
                        tb.Foreground = TextBoxNumCols.Foreground;
                        tb.Background = TextBoxNumCols.Background;
                    }
            }
        }
    }
}
