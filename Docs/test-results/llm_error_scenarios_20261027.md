<!-- 2026-10-27, Unity Editor (Edit modu), Ollama 0.32.14, RTX 5050 8 GB; model sicakken (ilk soguk istek 25 sn zaman asimina dustu) -->
### OllamaClient hata senaryoları (model qwen2.5:7b)

| Senaryo | Sonuç | Süre (ms) | Oyuncuya gösterilen | Ayrıntı |
|---|---|---|---|---|
| Sağlık kontrolü | None | 0 | qwen2.5:7b | yüklü modeller: qwen2.5:7b |
| Normal istek | None | 397 | {"reply": "Evet, duyabiliyorum."} | done_reason=stop, eval_count=15, server_ms=328 |
| Sunucu kapalı (sağlık) | ConnectionFailed | 97 | [Sistem] Ollama sunucusuna ulaşılamıyor. 'ollama serve' çalışıyor mu? | Cannot connect to destination host (http://localhost:11999/api/tags) |
| Sunucu kapalı (istek) | ConnectionFailed | 99 | [Sistem] Ollama sunucusuna ulaşılamıyor. 'ollama serve' çalışıyor mu? | Cannot connect to destination host (http://localhost:11999/api/chat) |
| Model yok (sağlık) | ModelNotFound | 99 | [Sistem] Model bulunamadı: ollama pull yazlab-olmayan-model:1b | 'yazlab-olmayan-model:1b' yüklü değil; yüklü modeller: qwen2.5:7b |
| Model yok (istek) | ModelNotFound | 99 | [Sistem] Model bulunamadı: ollama pull yazlab-olmayan-model:1b | HTTP 404: model 'yazlab-olmayan-model:1b' not found |
| Zaman aşımı (1 sn) | Timeout | 1092 | [Sistem] NPC yanıt vermedi (zaman aşımı). | Request timeout (http://localhost:11434/api/chat) |
| İstek sırasında iptal | Cancelled | 0 | [Sistem] İstek iptal edildi. | İstek iptal edildi. |
| Yerel olmayan adres | InvalidConfig | 0 | [Sistem] LLM ayarı geçersiz; yalnızca yerel/LAN Ollama adresi kullanılabilir. | 'llm.example.com' yerel/LAN adresi değil; yalnızca yerel Ollama sunucusu kullanılabilir. |
