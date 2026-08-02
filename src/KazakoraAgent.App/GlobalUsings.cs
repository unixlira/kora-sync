// UseWindowsForms=true (pro NotifyIcon) traz System.Drawing pro escopo
// implícito, que colide com System.Windows.Media (WPF) em Color/Brush —
// resolve a ambiguidade globalmente aqui em vez de qualificar toda
// referência nos arquivos que usam essas duas libs juntas.
global using Brush = System.Windows.Media.Brush;
global using Color = System.Windows.Media.Color;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
