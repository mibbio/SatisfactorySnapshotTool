namespace SatisfactorySnapshotTool.Views
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for BackupMasterView.xaml
    /// </summary>
    public partial class BackupView : UserControl
    {
        public BackupView()
        {
            InitializeComponent();
        }

        private void ScrollParent(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled && sender is UIElement uiElement)
            {
                e.Handled = true;
                var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
                args.RoutedEvent = MouseWheelEvent;
                args.Source = sender;
                var parent = uiElement;
                parent.RaiseEvent(args);
            }
        }
    }
}
