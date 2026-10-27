namespace Yazlab.LLM
{
    /// <summary>
    /// Modelin ham metnini <see cref="NpcResponse"/>'a çeviren ayrıştırıcı sözleşmesi. Uygulamalar istisna fırlatmaz.
    /// </summary>
    public interface INpcResponseParser
    {
        /// <summary>
        /// <paramref name="rawContent"/>'i ayrıştırır. Başarısızsa <c>false</c> döner, <paramref name="response"/> <c>null</c> olur
        /// ve <paramref name="error"/> Türkçe açıklama içerir.
        /// </summary>
        bool TryParse(string rawContent, out NpcResponse response, out string error);
    }
}
