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

namespace StS_GUI_Avalonia;

public partial class PasswordInputDec : Window
{
    private string _pwd = "";

    public PasswordInputDec()
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
        if (upperTextBox.Text != null) _pwd = upperTextBox.Text;
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