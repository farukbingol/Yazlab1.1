namespace Yazlab.LLM
{
    /// <summary>
    /// NPC'nin yanıtla birlikte istediği oyun aksiyonu. Oyun durumu yalnızca bu alanla değişir;
    /// <see cref="NpcResponse.Reply"/> metni asla aksiyon için taranmaz.
    /// </summary>
    public enum NpcAction
    {
        /// <summary>Aksiyon yok.</summary>
        None,

        /// <summary>Kapıyı aç (güven eşiği ayrıca denetlenir).</summary>
        OpenDoor,

        /// <summary>Oyuncuya saldır.</summary>
        Attack,

        /// <summary>Konuşmayı bitir.</summary>
        EndConversation
    }
}
