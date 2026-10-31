using System.Windows.Input;

namespace Nakawari.ViewModels;

/// <summary>One entry in a game's pacing menu.</summary>
public sealed record PacingOption(string Label, ICommand Command);
