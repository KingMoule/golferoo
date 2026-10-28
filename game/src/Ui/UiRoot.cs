using Godot;

namespace Golferoo.Game.Ui;

/// <summary>Racine plein écran de l'interface : porte <see cref="UiTheme.Theme"/> et retrouve un élément par son identifiant stable.</summary>
public partial class UiRoot : Control
{
    /// <summary>L'élément dont le <c>Name</c> est <paramref name="id"/> (une constante de <see cref="UiIds"/>), ou <c>null</c>.</summary>
    public Control? Find(string id) => FindChild(id, recursive: true, owned: false) as Control;
}
