using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2;

public partial class SaveGameWindow : Window
{
    private readonly ObservableCollection<SaveGameSummary> _slots;

    public SaveGameWindow(IEnumerable<SaveGameSummary> slots, string? initialName = null)
    {
        InitializeComponent();

        _slots = new ObservableCollection<SaveGameSummary>(slots);
        SlotsList.ItemsSource = _slots;

        if (_slots.Count > 0)
        {
            SlotsList.SelectedIndex = 0;
            NameTextBox.Text = _slots[0].Name;
        }
        else
        {
            NameTextBox.Text = initialName ?? GenerateDefaultName();
        }

        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    public string ResultName { get; private set; } = string.Empty;
    public string? ResultFilePath { get; private set; }

    private SaveGameSummary? SelectedSlot => SlotsList.SelectedItem as SaveGameSummary;

    private void OnSlotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedSlot is not null)
        {
            NameTextBox.Text = SelectedSlot.Name;
            NameTextBox.SelectAll();
        }
    }

    private void OnNewSlotClicked(object sender, RoutedEventArgs e)
    {
        SlotsList.SelectedIndex = -1;
        NameTextBox.Text = GenerateDefaultName();
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(this, "Please enter a name for the save.", "Save Game", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        ResultName = name;
        ResultFilePath = SelectedSlot?.FilePath;
        DialogResult = true;
    }

    private static string GenerateDefaultName() =>
        $"Save {DateTime.Now:yyyy-MM-dd HHmm}";
}
