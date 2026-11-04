namespace MonitorDimmer;

internal sealed record LanguageInfo(string Code, string NativeName);

internal static class Strings
{
    private static readonly string[] Keys =
    [
        "settings", "allDisplays", "allDisplaysHint", "brightness", "extraDim", "off", "hardwareControl", "softwareOnly",
        "rename", "identify", "refresh", "presets", "edit", "presetApplied", "detected", "noDisplays",
        "searching", "ddcHint", "open", "exit", "language", "theme", "deviceDefault", "light",
        "dark", "general", "appearance", "startWithWindows", "startWithWindowsDesc", "closeToTray", "closeToTrayDesc", "hotkeys",
        "hotkeysDesc", "save", "cancel", "name", "newPreset", "delete", "maxPresets", "trayHint",
        "renameHint", "sync", "startupError", "displays", "saveCurrent", "dimOff", "displayNames", "hardwareFailed",
        "primary", "about", "aboutTagline", "version", "createdBy", "donateText", "buyCoffee", "findMe", "support",
        "projectPage"
    ];

    public static readonly IReadOnlyList<LanguageInfo> Languages =
    [
        new("en", "English"), new("es", "Español"), new("tr", "Türkçe"), new("de", "Deutsch"), new("fr", "Français"),
        new("it", "Italiano"), new("pt", "Português"), new("ru", "Русский"), new("ja", "日本語"), new("ko", "한국어")
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> Table = new()
    {
        ["en"] = Map(
            "Settings", "All displays", "Moves every synced display together", "Brightness", "Extra dim", "Off", "Hardware brightness (DDC/CI)", "Software dim only",
            "Rename", "Identify displays", "Refresh", "Presets", "Edit", "Applied “{0}”", "{0} display(s) · {1} with hardware brightness", "No displays detected.",
            "Looking for displays…", "No hardware control — turn on DDC/CI in the monitor's menu, or use Extra dim.", "Open Monitor Dimmer", "Exit", "Language", "Theme", "Device default", "Light",
            "Dark", "General", "Appearance", "Start with Windows", "Opens quietly in the notification area when you sign in.", "Keep running in the notification area", "Closing the window hides it from the taskbar and Alt+Tab instead of exiting.", "Keyboard shortcuts",
            "Ctrl+Alt+Page Up / Page Down changes the brightness of synced displays.", "Save", "Cancel", "Name", "New preset", "Delete", "You can keep up to 8 presets.", "Monitor Dimmer is still running. Click the icon to open it, right-click for presets.",
            "Enter to save · Esc to cancel · leave empty to reset", "Sync with All displays", "Start with Windows only works from the built .exe file.", "Displays", "Save current", "Turn off extra dim", "Display names", "{0} didn't accept the change. Check DDC/CI in its on-screen menu.",
            "Main display", "About", "Brightness for every monitor, right from the tray.", "Version {0}", "Created with {0} by {1}", "This app is free for everyone, and I plan to keep it that way.\nIf Monitor Dimmer made your day a little easier and you'd like to support future updates, a small donation would mean a lot.", "Buy me a coffee", "Find me online", "Support Monitor Dimmer",
            "Open the project page on GitHub"),
        ["es"] = Map(
            "Configuración", "Todas las pantallas", "Ajusta juntas todas las pantallas sincronizadas", "Brillo", "Atenuación extra", "Desactivado", "Brillo por hardware (DDC/CI)", "Solo atenuación por software",
            "Cambiar nombre", "Identificar pantallas", "Actualizar", "Preajustes", "Editar", "Se aplicó «{0}»", "{0} pantalla(s) · {1} con brillo por hardware", "No se detectaron pantallas.",
            "Buscando pantallas…", "Sin control por hardware: activa DDC/CI en el menú del monitor o usa Atenuación extra.", "Abrir Monitor Dimmer", "Salir", "Idioma", "Tema", "Predeterminado del dispositivo", "Claro",
            "Oscuro", "General", "Apariencia", "Iniciar con Windows", "Se abre discretamente en el área de notificación al iniciar sesión.", "Seguir ejecutándose en el área de notificación", "Al cerrar la ventana se oculta de la barra de tareas y de Alt+Tab en lugar de salir.", "Atajos de teclado",
            "Ctrl+Alt+Re Pág / Av Pág cambia el brillo de las pantallas sincronizadas.", "Guardar", "Cancelar", "Nombre", "Nuevo preajuste", "Eliminar", "Puedes guardar hasta 8 preajustes.", "Monitor Dimmer sigue en ejecución. Haz clic en el icono para abrirlo o clic derecho para los preajustes.",
            "Enter para guardar · Esc para cancelar · vacío para restablecer", "Sincronizar con Todas las pantallas", "Iniciar con Windows solo funciona desde el archivo .exe compilado.", "Pantallas", "Guardar actual", "Desactivar atenuación extra", "Nombres de pantallas", "{0} no aceptó el cambio. Comprueba DDC/CI en su menú en pantalla.",
            "Pantalla principal", "Acerca de", "El brillo de cada monitor, desde la bandeja del sistema.", "Versión {0}", "Hecho con {0} por {1}", "Esta aplicación es gratuita para todos y quiero que siga así.\nSi Monitor Dimmer te ha hecho el día un poco más fácil y quieres apoyar futuras actualizaciones, una pequeña donación significaría mucho.", "Invítame a un café", "Encuéntrame en línea", "Apoyar Monitor Dimmer",
            "Abrir la página del proyecto en GitHub"),
        ["tr"] = Map(
            "Ayarlar", "Tüm ekranlar", "Senkronize tüm ekranları birlikte ayarlar", "Parlaklık", "Ek karartma", "Kapalı", "Donanım parlaklığı (DDC/CI)", "Yalnızca yazılım karartma",
            "Yeniden adlandır", "Ekranları tanımla", "Yenile", "Hazır ayarlar", "Düzenle", "“{0}” uygulandı", "{0} ekran · {1} tanesi donanım parlaklığını destekliyor", "Ekran algılanmadı.",
            "Ekranlar aranıyor…", "Donanım denetimi yok — monitör menüsünden DDC/CI'ı açın veya Ek karartma'yı kullanın.", "Monitor Dimmer'ı aç", "Çıkış", "Dil", "Tema", "Cihaz varsayılanı", "Açık",
            "Koyu", "Genel", "Görünüm", "Windows ile başlat", "Oturum açtığınızda bildirim alanında sessizce açılır.", "Bildirim alanında çalışmaya devam et", "Pencereyi kapatmak uygulamadan çıkmak yerine onu görev çubuğundan ve Alt+Tab'dan gizler.", "Klavye kısayolları",
            "Ctrl+Alt+Page Up / Page Down senkronize ekranların parlaklığını değiştirir.", "Kaydet", "İptal", "Ad", "Yeni hazır ayar", "Sil", "En fazla 8 hazır ayar saklayabilirsiniz.", "Monitor Dimmer çalışmaya devam ediyor. Açmak için simgeye tıklayın, hazır ayarlar için sağ tıklayın.",
            "Kaydetmek için Enter · iptal için Esc · sıfırlamak için boş bırakın", "Tüm ekranlarla senkronize et", "Windows ile başlat yalnızca derlenmiş .exe dosyasından çalışır.", "Ekranlar", "Mevcut olanı kaydet", "Ek karartmayı kapat", "Ekran adları", "{0} değişikliği kabul etmedi. Ekran menüsünden DDC/CI'ı kontrol edin.",
            "Ana ekran", "Hakkında", "Her monitörün parlaklığı, doğrudan bildirim alanından.", "Sürüm {0}", "{1} tarafından {0} ile yapıldı", "Bu uygulama herkes için ücretsiz ve öyle kalmasını istiyorum.\nMonitor Dimmer gününüzü biraz kolaylaştırdıysa ve gelecek güncellemeleri desteklemek isterseniz, küçük bir bağış çok şey ifade eder.", "Bana bir kahve ısmarla", "Beni internette bulun", "Monitor Dimmer'ı destekle",
            "GitHub'daki proje sayfasını aç"),
        ["de"] = Map(
            "Einstellungen", "Alle Bildschirme", "Passt alle synchronisierten Bildschirme gemeinsam an", "Helligkeit", "Zusätzlich abdunkeln", "Aus", "Hardware-Helligkeit (DDC/CI)", "Nur Software-Abdunklung",
            "Umbenennen", "Bildschirme identifizieren", "Aktualisieren", "Voreinstellungen", "Bearbeiten", "„{0}“ angewendet", "{0} Bildschirm(e) · {1} mit Hardware-Helligkeit", "Keine Bildschirme erkannt.",
            "Suche nach Bildschirmen…", "Keine Hardware-Steuerung – aktivieren Sie DDC/CI im Monitormenü oder nutzen Sie „Zusätzlich abdunkeln“.", "Monitor Dimmer öffnen", "Beenden", "Sprache", "Design", "Gerätestandard", "Hell",
            "Dunkel", "Allgemein", "Darstellung", "Mit Windows starten", "Startet nach der Anmeldung unauffällig im Infobereich.", "Im Infobereich weiterlaufen", "Beim Schließen wird das Fenster aus Taskleiste und Alt+Tab ausgeblendet, statt die App zu beenden.", "Tastenkombinationen",
            "Strg+Alt+Bild auf / Bild ab ändert die Helligkeit synchronisierter Bildschirme.", "Speichern", "Abbrechen", "Name", "Neue Voreinstellung", "Löschen", "Sie können bis zu 8 Voreinstellungen behalten.", "Monitor Dimmer läuft weiter. Klicken Sie auf das Symbol zum Öffnen, Rechtsklick für Voreinstellungen.",
            "Enter zum Speichern · Esc zum Abbrechen · leer lassen zum Zurücksetzen", "Mit „Alle Bildschirme“ synchronisieren", "„Mit Windows starten“ funktioniert nur mit der erstellten .exe-Datei.", "Bildschirme", "Aktuelle speichern", "Zusätzliche Abdunklung aus", "Bildschirmnamen", "{0} hat die Änderung nicht übernommen. Prüfen Sie DDC/CI im Bildschirmmenü.",
            "Hauptbildschirm", "Info", "Helligkeit für jeden Monitor, direkt aus dem Infobereich.", "Version {0}", "Mit {0} gemacht von {1}", "Diese App ist für alle kostenlos, und das soll auch so bleiben.\nWenn Monitor Dimmer Ihren Tag etwas leichter gemacht hat und Sie künftige Updates unterstützen möchten, würde mir eine kleine Spende viel bedeuten.", "Spendier mir einen Kaffee", "Mich im Netz finden", "Monitor Dimmer unterstützen",
            "Projektseite auf GitHub öffnen"),
        ["fr"] = Map(
            "Paramètres", "Tous les écrans", "Règle ensemble tous les écrans synchronisés", "Luminosité", "Assombrissement", "Désactivé", "Luminosité matérielle (DDC/CI)", "Assombrissement logiciel uniquement",
            "Renommer", "Identifier les écrans", "Actualiser", "Préréglages", "Modifier", "« {0} » appliqué", "{0} écran(s) · {1} avec luminosité matérielle", "Aucun écran détecté.",
            "Recherche des écrans…", "Pas de contrôle matériel : activez DDC/CI dans le menu du moniteur ou utilisez l'assombrissement.", "Ouvrir Monitor Dimmer", "Quitter", "Langue", "Thème", "Par défaut de l'appareil", "Clair",
            "Sombre", "Général", "Apparence", "Démarrer avec Windows", "S'ouvre discrètement dans la zone de notification à la connexion.", "Continuer dans la zone de notification", "Fermer la fenêtre la masque de la barre des tâches et d'Alt+Tab au lieu de quitter.", "Raccourcis clavier",
            "Ctrl+Alt+Page préc. / Page suiv. modifie la luminosité des écrans synchronisés.", "Enregistrer", "Annuler", "Nom", "Nouveau préréglage", "Supprimer", "Vous pouvez conserver jusqu'à 8 préréglages.", "Monitor Dimmer est toujours actif. Cliquez sur l'icône pour l'ouvrir, clic droit pour les préréglages.",
            "Entrée pour enregistrer · Échap pour annuler · vide pour réinitialiser", "Synchroniser avec Tous les écrans", "Démarrer avec Windows ne fonctionne qu'avec le fichier .exe compilé.", "Écrans", "Enregistrer l'actuel", "Désactiver l'assombrissement", "Noms des écrans", "{0} n'a pas accepté la modification. Vérifiez DDC/CI dans son menu.",
            "Écran principal", "À propos", "La luminosité de chaque écran, depuis la zone de notification.", "Version {0}", "Créé avec {0} par {1}", "Cette application est gratuite pour tous, et je compte bien qu'elle le reste.\nSi Monitor Dimmer vous a facilité la journée et que vous souhaitez soutenir les prochaines mises à jour, un petit don me ferait très plaisir.", "Offrez-moi un café", "Me retrouver en ligne", "Soutenir Monitor Dimmer",
            "Ouvrir la page du projet sur GitHub"),
        ["it"] = Map(
            "Impostazioni", "Tutti gli schermi", "Regola insieme tutti gli schermi sincronizzati", "Luminosità", "Oscuramento extra", "Disattivato", "Luminosità hardware (DDC/CI)", "Solo oscuramento software",
            "Rinomina", "Identifica schermi", "Aggiorna", "Preimpostazioni", "Modifica", "Applicato «{0}»", "{0} schermo/i · {1} con luminosità hardware", "Nessuno schermo rilevato.",
            "Ricerca degli schermi…", "Nessun controllo hardware: attiva DDC/CI nel menu del monitor o usa l'oscuramento extra.", "Apri Monitor Dimmer", "Esci", "Lingua", "Tema", "Predefinito del dispositivo", "Chiaro",
            "Scuro", "Generale", "Aspetto", "Avvia con Windows", "Si apre in modo discreto nell'area di notifica all'accesso.", "Continua nell'area di notifica", "Chiudere la finestra la nasconde dalla barra delle applicazioni e da Alt+Tab invece di uscire.", "Scorciatoie da tastiera",
            "Ctrl+Alt+Pag su / Pag giù cambia la luminosità degli schermi sincronizzati.", "Salva", "Annulla", "Nome", "Nuova preimpostazione", "Elimina", "Puoi conservare fino a 8 preimpostazioni.", "Monitor Dimmer è ancora in esecuzione. Fai clic sull'icona per aprirlo, clic destro per le preimpostazioni.",
            "Invio per salvare · Esc per annullare · vuoto per ripristinare", "Sincronizza con Tutti gli schermi", "Avvia con Windows funziona solo dal file .exe compilato.", "Schermi", "Salva attuale", "Disattiva oscuramento extra", "Nomi degli schermi", "{0} non ha accettato la modifica. Controlla DDC/CI nel suo menu.",
            "Schermo principale", "Informazioni", "La luminosità di ogni monitor, direttamente dall'area di notifica.", "Versione {0}", "Creato con {0} da {1}", "Questa app è gratuita per tutti e intendo mantenerla così.\nSe Monitor Dimmer ti ha reso la giornata un po' più semplice e vuoi sostenere i prossimi aggiornamenti, una piccola donazione significherebbe molto.", "Offrimi un caffè", "Trovami online", "Sostieni Monitor Dimmer",
            "Apri la pagina del progetto su GitHub"),
        ["pt"] = Map(
            "Configurações", "Todas as telas", "Ajusta juntas todas as telas sincronizadas", "Brilho", "Escurecimento extra", "Desligado", "Brilho por hardware (DDC/CI)", "Somente escurecimento por software",
            "Renomear", "Identificar telas", "Atualizar", "Predefinições", "Editar", "“{0}” aplicada", "{0} tela(s) · {1} com brilho por hardware", "Nenhuma tela detectada.",
            "Procurando telas…", "Sem controle por hardware: ative o DDC/CI no menu do monitor ou use o escurecimento extra.", "Abrir Monitor Dimmer", "Sair", "Idioma", "Tema", "Padrão do dispositivo", "Claro",
            "Escuro", "Geral", "Aparência", "Iniciar com o Windows", "Abre discretamente na área de notificação ao entrar.", "Continuar na área de notificação", "Fechar a janela a oculta da barra de tarefas e do Alt+Tab em vez de sair.", "Atalhos de teclado",
            "Ctrl+Alt+Page Up / Page Down altera o brilho das telas sincronizadas.", "Salvar", "Cancelar", "Nome", "Nova predefinição", "Excluir", "Você pode manter até 8 predefinições.", "O Monitor Dimmer continua em execução. Clique no ícone para abri-lo ou com o botão direito para predefinições.",
            "Enter para salvar · Esc para cancelar · vazio para redefinir", "Sincronizar com Todas as telas", "Iniciar com o Windows só funciona a partir do arquivo .exe compilado.", "Telas", "Salvar atual", "Desligar escurecimento extra", "Nomes das telas", "{0} não aceitou a alteração. Verifique o DDC/CI no menu da tela.",
            "Tela principal", "Sobre", "O brilho de cada monitor, direto da área de notificação.", "Versão {0}", "Feito com {0} por {1}", "Este app é gratuito para todos, e pretendo mantê-lo assim.\nSe o Monitor Dimmer deixou seu dia um pouco mais fácil e você quiser apoiar futuras atualizações, uma pequena doação faria muita diferença.", "Me pague um café", "Me encontre online", "Apoiar o Monitor Dimmer",
            "Abrir a página do projeto no GitHub"),
        ["ru"] = Map(
            "Настройки", "Все экраны", "Регулирует все синхронизированные экраны вместе", "Яркость", "Доп. затемнение", "Выкл.", "Аппаратная яркость (DDC/CI)", "Только программное затемнение",
            "Переименовать", "Определить экраны", "Обновить", "Пресеты", "Изменить", "Применено: «{0}»", "Экранов: {0} · с аппаратной яркостью: {1}", "Экраны не обнаружены.",
            "Поиск экранов…", "Нет аппаратного управления — включите DDC/CI в меню монитора или используйте доп. затемнение.", "Открыть Monitor Dimmer", "Выход", "Язык", "Тема", "Как в системе", "Светлая",
            "Тёмная", "Общие", "Оформление", "Запускать с Windows", "Незаметно запускается в области уведомлений после входа.", "Работать в области уведомлений", "При закрытии окно скрывается с панели задач и из Alt+Tab, а приложение продолжает работать.", "Сочетания клавиш",
            "Ctrl+Alt+Page Up / Page Down меняет яркость синхронизированных экранов.", "Сохранить", "Отмена", "Название", "Новый пресет", "Удалить", "Можно хранить до 8 пресетов.", "Monitor Dimmer продолжает работать. Нажмите на значок, чтобы открыть, правой кнопкой — пресеты.",
            "Enter — сохранить · Esc — отмена · пусто — сбросить", "Синхронизировать со «Все экраны»", "Запуск с Windows работает только из собранного .exe файла.", "Экраны", "Сохранить текущее", "Выключить доп. затемнение", "Названия экранов", "{0} не принял изменение. Проверьте DDC/CI в меню монитора.",
            "Основной экран", "О программе", "Яркость каждого монитора прямо из области уведомлений.", "Версия {0}", "Сделано с {0} — {1}", "Это приложение бесплатно для всех, и я хочу, чтобы так и оставалось.\nЕсли Monitor Dimmer сделал ваш день чуточку проще и вы хотите поддержать будущие обновления, небольшое пожертвование очень много для меня значит.", "Угостить кофе", "Я в сети", "Поддержать Monitor Dimmer",
            "Открыть страницу проекта на GitHub"),
        ["ja"] = Map(
            "設定", "すべてのディスプレイ", "同期中のディスプレイをまとめて調整します", "明るさ", "追加の減光", "オフ", "ハードウェア輝度 (DDC/CI)", "ソフトウェア減光のみ",
            "名前を変更", "ディスプレイを識別", "更新", "プリセット", "編集", "「{0}」を適用しました", "{0} 台のディスプレイ · {1} 台がハードウェア輝度に対応", "ディスプレイが見つかりません。",
            "ディスプレイを検索しています…", "ハードウェア制御なし — モニターのメニューで DDC/CI を有効にするか、追加の減光を使用してください。", "Monitor Dimmer を開く", "終了", "言語", "テーマ", "デバイスの既定", "ライト",
            "ダーク", "全般", "外観", "Windows と同時に起動", "サインイン時に通知領域で静かに起動します。", "通知領域で実行を続ける", "ウィンドウを閉じると終了せず、タスクバーと Alt+Tab から非表示になります。", "キーボード ショートカット",
            "Ctrl+Alt+Page Up / Page Down で同期中のディスプレイの明るさを変更します。", "保存", "キャンセル", "名前", "新しいプリセット", "削除", "プリセットは 8 個まで保存できます。", "Monitor Dimmer は実行中です。アイコンをクリックすると開き、右クリックでプリセットを選べます。",
            "Enter で保存 · Esc でキャンセル · 空欄でリセット", "「すべてのディスプレイ」と同期", "Windows と同時に起動は、ビルドした .exe ファイルからのみ使用できます。", "ディスプレイ", "現在の設定を保存", "追加の減光をオフ", "ディスプレイ名", "{0} は変更を受け付けませんでした。画面メニューで DDC/CI を確認してください。",
            "メイン ディスプレイ", "バージョン情報", "すべてのモニターの明るさを、通知領域からすぐに。", "バージョン {0}", "{1} が {0} を込めて制作", "このアプリは誰でも無料で使えます。これからもそのつもりです。\nMonitor Dimmer が少しでも役に立ち、今後のアップデートを応援していただけるなら、少額の寄付がとても励みになります。", "コーヒーをおごる", "オンラインで見つける", "Monitor Dimmer を応援",
            "GitHub のプロジェクト ページを開く"),
        ["ko"] = Map(
            "설정", "모든 디스플레이", "동기화된 모든 디스플레이를 함께 조정합니다", "밝기", "추가 어둡게", "끔", "하드웨어 밝기 (DDC/CI)", "소프트웨어 어둡게만 가능",
            "이름 바꾸기", "디스플레이 식별", "새로 고침", "프리셋", "편집", "“{0}” 적용됨", "디스플레이 {0}개 · 하드웨어 밝기 지원 {1}개", "디스플레이가 감지되지 않았습니다.",
            "디스플레이를 찾는 중…", "하드웨어 제어 없음 — 모니터 메뉴에서 DDC/CI를 켜거나 추가 어둡게를 사용하세요.", "Monitor Dimmer 열기", "종료", "언어", "테마", "장치 기본값", "밝게",
            "어둡게", "일반", "모양", "Windows와 함께 시작", "로그인하면 알림 영역에서 조용히 시작됩니다.", "알림 영역에서 계속 실행", "창을 닫으면 종료하는 대신 작업 표시줄과 Alt+Tab에서 숨깁니다.", "바로 가기 키",
            "Ctrl+Alt+Page Up / Page Down으로 동기화된 디스플레이의 밝기를 변경합니다.", "저장", "취소", "이름", "새 프리셋", "삭제", "프리셋은 최대 8개까지 저장할 수 있습니다.", "Monitor Dimmer가 계속 실행 중입니다. 아이콘을 클릭하면 열리고, 마우스 오른쪽 버튼으로 프리셋을 선택할 수 있습니다.",
            "Enter 저장 · Esc 취소 · 비워 두면 초기화", "모든 디스플레이와 동기화", "Windows와 함께 시작은 빌드된 .exe 파일에서만 작동합니다.", "디스플레이", "현재 상태 저장", "추가 어둡게 끄기", "디스플레이 이름", "{0}이(가) 변경을 적용하지 않았습니다. 화면 메뉴에서 DDC/CI를 확인하세요.",
            "주 디스플레이", "정보", "모든 모니터의 밝기를 알림 영역에서 바로.", "버전 {0}", "{0}을 담아 {1} 제작", "이 앱은 누구나 무료로 사용할 수 있고, 앞으로도 그럴 예정입니다.\nMonitor Dimmer가 하루를 조금 더 편하게 해 주었고 앞으로의 업데이트를 응원하고 싶다면, 작은 후원이 큰 힘이 됩니다.", "커피 한 잔 사주기", "온라인에서 만나요", "Monitor Dimmer 후원하기",
            "GitHub 프로젝트 페이지 열기")
    };

    public static bool IsSupported(string? code) => code is not null && Table.ContainsKey(code);

    public static string T(string language, string key, params object[] values)
    {
        var text = Table.TryGetValue(language, out var map) && map.TryGetValue(key, out var translated)
            ? translated
            : Table["en"].TryGetValue(key, out var english) ? english : key;
        return values.Length == 0 ? text : string.Format(text, values);
    }

    /// <summary>Returns problems found in the translation table (used by --selftest).</summary>
    public static IEnumerable<string> Validate()
    {
        foreach (var (language, map) in Table)
        {
            if (map.Count != Keys.Length)
            {
                yield return $"{language}: {map.Count} of {Keys.Length} keys";
            }

            foreach (var key in Keys)
            {
                var english = Table["en"][key];
                if (!map.TryGetValue(key, out var text))
                {
                    continue;
                }

                for (var i = 0; i < 2; i++)
                {
                    if (english.Contains($"{{{i}}}") != text.Contains($"{{{i}}}"))
                    {
                        yield return $"{language}.{key}: placeholder {{{i}}} mismatch";
                    }
                }
            }
        }
    }

    private static Dictionary<string, string> Map(params string[] values)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < Math.Min(values.Length, Keys.Length); i++)
        {
            map[Keys[i]] = values[i];
        }

        return map;
    }
}
