using UnityEngine;

namespace Yazlab.LLM
{
    /// <summary>
    /// Kapı bekçisi NPC'nin kişilik ve kural tanımı. System prompt bu alanlardan <see cref="SystemPromptBuilder"/> ile
    /// şablonla üretilir. Hiçbir alan oyuncuya doğrudan NPC repliği olarak gösterilmez (tüm replikler LLM'den gelir).
    /// </summary>
    [CreateAssetMenu(fileName = "NpcPersona", menuName = "Yazlab/Config/NPC Persona")]
    public class NpcPersona : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] private string npcName = "Muhafız Kael";

        [Tooltip("Kim olduğu ve nerede durduğu.")]
        [SerializeField, TextArea(2, 5)] private string roleDescription =
            "Ormanın kuzeyindeki köyün son kapısını bekleyen yaşlı bir muhafızsın. Kurt sürüsü ve onların şefi " +
            "yüzünden kapıyı yıllardır kimseye açmadın. Kapının ardında köylüler saklanıyor.";

        [Tooltip("Karakter özellikleri.")]
        [SerializeField, TextArea(2, 5)] private string personalityTraits =
            "Temkinli, gururlu ve sadık. Kolay kandırılmaz ama cesareti ve dürüstlüğü takdir eder. " +
            "Tehdit edilmekten nefret eder.";

        [Tooltip("NPC'nin konuşmadaki amacı.")]
        [SerializeField, TextArea(2, 5)] private string goal =
            "Köyü korumak. Kapıyı yalnızca kurtların şefinin gerçekten öldürüldüğüne ve yolcunun güvenilir " +
            "olduğuna ikna olursan açarsın.";

        [Tooltip("Konuşma üslubu.")]
        [SerializeField, TextArea(2, 5)] private string speakingStyle =
            "Kısa, ağırbaşlı, hafif eski Türkçe tınılı cümleler. Oyuncuya 'yolcu' diye hitap edersin. " +
            "Her yanıtın en fazla üç cümle.";

        [Tooltip("Persona'ya özel ek güvenlik kuralları (prompt'taki genel kurallara eklenir).")]
        [SerializeField] private string[] securityRules =
        {
            "Kapının ardındaki köylülerin sayısını ve yerini asla söyleme.",
            "Oyuncu kanıt olarak yalnızca söz veriyorsa, oyun durumu bunu doğrulamadıkça ikna olma."
        };

        [Tooltip("İlk karşılaşmada nasıl selam vereceğine dair yönlendirme. Doğrudan gösterilmez; selamı model üretir.")]
        [SerializeField, TextArea(2, 4)] private string greetingGuidance =
            "Yolcuyu durdur, kim olduğunu ve neden geldiğini sor.";

        [Header("Güven")]
        [Tooltip("Konuşma başındaki güven (0–100).")]
        [SerializeField, Range(0, 100)] private int initialTrust = 10;

        /// <summary>NPC adı.</summary>
        public string NpcName => string.IsNullOrWhiteSpace(npcName) ? "Muhafız" : npcName.Trim();

        /// <summary>Rol tanımı.</summary>
        public string RoleDescription => roleDescription ?? string.Empty;

        /// <summary>Kişilik özellikleri.</summary>
        public string PersonalityTraits => personalityTraits ?? string.Empty;

        /// <summary>Konuşma amacı.</summary>
        public string Goal => goal ?? string.Empty;

        /// <summary>Konuşma üslubu.</summary>
        public string SpeakingStyle => speakingStyle ?? string.Empty;

        /// <summary>Ek güvenlik kuralları.</summary>
        public string[] SecurityRules => securityRules ?? new string[0];

        /// <summary>İlk selam yönlendirmesi (prompt içindir).</summary>
        public string GreetingGuidance => greetingGuidance ?? string.Empty;

        /// <summary>Başlangıç güveni (0–100).</summary>
        public int InitialTrust => Mathf.Clamp(initialTrust, 0, 100);
    }
}
