/*SchildToSchule (StS) - Programm zur Verwaltung von Schüler:innen/Lehrkräfte und Kursdaten
   Copyright (C) 2026 Kay-Patrick Wittbold

   This program is free software: you can redistribute it and/or modify
   it under the terms of the GNU General Public License as published by
   the Free Software Foundation, either version 3 of the License, or
   (at your option) any later version.

   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU General Public License for more details.

   You should have received a copy of the GNU General Public License
   along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace StS_GUI_Avalonia;

public partial class PasswordInputEnc : Window
{
    private string _pwd = "";

    public PasswordInputEnc()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ButtonOK_OnClick(object? sender, RoutedEventArgs e)
    {
        var upperTextBox = this.GetControl<TextBox>("pwInputUpper");
        var lowerTextBox = this.GetControl<TextBox>("pwInputLower");
        if (upperTextBox.Text != lowerTextBox.Text || string.IsNullOrEmpty(upperTextBox.Text))
        {
            upperTextBox.BorderBrush = lowerTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            return;
        }

        upperTextBox.BorderBrush = lowerTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 255, 0));
        _pwd = upperTextBox.Text;
        Close();
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        _pwd = "";
        Close();
    }

    public async Task<string?> ShowPwdDialog(Window owner)
    {
        await ShowDialog(owner);
        return _pwd;
    }
}