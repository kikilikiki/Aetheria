namespace Aetheria.Shared.Enums;

/// <summary>Canal d'un <c>ChatMessagePacket</c> — tchat global (tout le monde), tchat de guilde (membres de la même guilde uniquement) ou message privé (voir GDD/demande utilisateur — "discussion privée" avec un ami, <see cref="ChatMessagePacket.TargetCharacterName"/>).</summary>
public enum ChatChannel
{
    Global,
    Guild,
    Prive,
}
