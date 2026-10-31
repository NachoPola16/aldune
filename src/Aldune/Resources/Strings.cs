using Aldune.Core;

namespace Aldune.Resources;

/// <summary>
/// Todos los textos de la interfaz, en todos los idiomas, uno al lado del otro: inglés, español,
/// alemán, francés, portugués de Brasil, italiano, japonés y chino simplificado (ver <see cref="UiLanguages"/>).
///
/// No son ficheros .resx: con dos idiomas y un puñado de cadenas, un diccionario a mano evita la
/// maquinaria de generación de código de recursos (que además depende de herramientas de Visual
/// Studio que no están garantizadas en cualquier máquina donde se compile esto) y sobre todo evita
/// el error más común de .resx — traducir un fichero satélite y olvidarse del otro — porque las dos
/// versiones de cada texto están en la misma línea.
///
/// <c>Current</c> se fija una sola vez al arrancar (<c>App.OnStartup</c>), antes de construir
/// cualquier ventana, a partir de <c>AppSettings.Language</c>. Los enlaces <c>{x:Static}</c> del
/// XAML se resuelven en el momento en que cada ventana se construye — por eso un cambio de idioma
/// en Ajustes no se ve en las ventanas ya abiertas hasta reiniciar la app: no hay nada más simple y
/// consistente que ofrecer sin meter un sistema de notificación de cambios en cada TextBlock.
/// </summary>
public static class Strings
{
    /// <summary>Código de <see cref="UiLanguages"/>. Por defecto inglés; <c>App.OnStartup</c> lo resuelve de verdad.</summary>
    public static string Current { get; set; } = "en";

    // Orden fijo: inglés, español, alemán, francés, portugués (de Brasil), italiano, japonés, chino simplificado. Una traducción que falte
    // cae al inglés, nunca a un texto vacío.
    private static string T(string en, string es, string? de = null, string? fr = null, string? pt = null, string? it = null, string? ja = null, string? zh = null) => Current switch
    {
        "es" => es,
        "de" => de ?? en,
        "fr" => fr ?? en,
        "pt" => pt ?? en,
        "it" => it ?? en,
        "ja" => ja ?? en,
        "zh" => zh ?? en,
        _ => en
    };

    // --- General / compartido entre ventanas -----------------------------------------------------

    public static string AppName => BrandIdentity.AppName;
    public static string ReminderToastTitle => T("Reminder", "Recordatorio",
        "Erinnerung", "Rappel", "Lembrete", "Promemoria", "リマインダー", "提醒");
    public static string ReminderIncludeTasksCheckbox => T("Show pending tasks in the reminder", "Mostrar las tareas pendientes en el recordatorio",
        "Offene Aufgaben in der Erinnerung anzeigen", "Afficher les tâches en attente dans le rappel", "Mostrar as tarefas pendentes no lembrete", "Mostra le attività in sospeso nel promemoria", "リマインダーに未完了のタスクを表示する", "在提醒中显示待办任务");
    public static string ReminderIncludeTasksHint => T("The reminder is always for the whole note. This only decides whether its notification lists the first tasks left to do.",
        "El recordatorio es siempre de la nota entera. Esto solo decide si el aviso enseña las primeras tareas que quedan por hacer.",
        "Die Erinnerung gilt immer für die ganze Notiz. Das entscheidet nur, ob die Benachrichtigung die ersten offenen Aufgaben auflistet.",
        "Le rappel concerne toujours toute la note. Cela décide seulement si la notification liste les premières tâches restantes.",
        "O lembrete é sempre da nota inteira. Isto só decide se o aviso lista as primeiras tarefas que faltam.", "Il promemoria riguarda sempre l'intera nota. Qui si decide solo se la notifica elenca le prime attività ancora da fare.", "リマインダーは常にノート全体が対象です。ここでは、通知に残りの最初のタスクを一覧表示するかどうかだけを決めます。", "提醒始终针对整条笔记。这里只决定通知中是否列出前几项尚未完成的任务。");
    public static string SkinColorNotice(SkinColorNoticeKind kind) => kind switch
    {
        SkinColorNoticeKind.GrayStripe => T(
            "With this look, black, white and grey show as the same light grey stripe.",
            "Con este aspecto, el negro, el blanco y los grises se ven como la misma franja gris clara.",
            "Mit diesem Aussehen erscheinen Schwarz, Weiß und Grau als derselbe hellgraue Streifen.",
            "Avec cet aspect, le noir, le blanc et les gris apparaissent comme la même bande gris clair.",
            "Com este visual, preto, branco e cinzas aparecem como a mesma faixa cinza-clara.", "Con questo aspetto, nero, bianco e grigi appaiono come la stessa striscia grigio chiaro.", "このデザインでは、黒・白・グレーはすべて同じ薄いグレーの帯で表示されます。", "使用此外观时，黑色、白色和灰色都显示为同一条浅灰色条带。"),
        SkinColorNoticeKind.ColorsNotShown => T(
            "This look doesn't show note colors.",
            "Este aspecto no muestra los colores de las notas.",
            "Dieses Aussehen zeigt keine Notizfarben.",
            "Cet aspect n'affiche pas les couleurs des notes.",
            "Este visual não mostra as cores das notas.", "Questo aspetto non mostra i colori delle note.", "このデザインではノートの色は表示されません。", "此外观不显示笔记颜色。"),
        SkinColorNoticeKind.ChannelColors => T(
            "With this look each note takes the color of its channel; to use one color, turn on \"same color for every note\".",
            "Con este aspecto cada nota toma el color de su canal; para usar un color, activa \"mismo color en todas las notas\".",
            "Mit diesem Aussehen übernimmt jede Notiz die Farbe ihres Kanals; für eine einheitliche Farbe \"gleiche Farbe für alle Notizen\" aktivieren.",
            "Avec cet aspect, chaque note prend la couleur de son canal ; pour une seule couleur, activez « même couleur pour toutes les notes ».",
            "Com este visual cada nota usa a cor do seu canal; para usar uma cor, ative \"mesma cor em todas as notas\".", "Con questo aspetto ogni nota prende il colore del suo canale; per usare un solo colore, attiva \"stesso colore per tutte le note\".", "このデザインでは、各ノートがそのチャンネルの色になります。1色にしたい場合は「すべてのノートを同じ色にする」をオンにしてください。", "使用此外观时，每条笔记采用其频道的颜色；若要使用单一颜色，请开启“所有笔记使用同一颜色”。"),
        _ => "",
    };
    // --- Sonidos ---
    public static string SoundsEnabledCheckbox => T("Play sounds", "Activar sonidos", "Töne abspielen", "Activer les sons", "Ativar sons", "Riproduci suoni", "サウンドを再生する", "播放声音");
    public static string SoundsHint => T(
        "Off by default. Each event has its own sound, or none: turn the switch off to mute everything at once.",
        "Apagado de fábrica. Cada evento tiene su propio sonido, o ninguno: apaga el interruptor para silenciar todo de golpe.",
        "Standardmäßig aus. Jedes Ereignis hat seinen eigenen Ton oder keinen: Schalter ausschalten, um alles stummzuschalten.",
        "Désactivé par défaut. Chaque événement a son propre son, ou aucun : désactivez l'interrupteur pour tout couper d'un coup.",
        "Desligado por padrão. Cada evento tem seu próprio som, ou nenhum: desligue o interruptor para silenciar tudo de uma vez.", "Disattivato per impostazione predefinita. Ogni evento ha il suo suono, oppure nessuno: disattiva l'interruttore per silenziare tutto in una volta.", "初期設定ではオフです。イベントごとに専用のサウンドを設定できます（なしも可）。スイッチをオフにすると、すべてをまとめてミュートできます。", "默认关闭。每个事件都有各自的声音，也可以不设声音：关闭开关即可一次性全部静音。");
    public static string SoundEventName(SoundEvent soundEvent) => soundEvent switch
    {
        SoundEvent.Reminder => T("Reminder", "Recordatorio", "Erinnerung", "Rappel", "Lembrete", "Promemoria", "リマインダー", "提醒"),
        SoundEvent.SyncDone => T("Sync finished", "Sincronización terminada", "Synchronisierung abgeschlossen", "Synchronisation terminée", "Sincronização concluída", "Sincronizzazione completata", "同期が完了しました", "同步完成"),
        SoundEvent.SyncFailed => T("Sync failed", "Sincronización fallida", "Synchronisierung fehlgeschlagen", "Échec de la synchronisation", "Falha na sincronização", "Sincronizzazione non riuscita", "同期に失敗しました", "同步失败"),
        SoundEvent.NoteCreated => T("New note", "Nota nueva", "Neue Notiz", "Nouvelle note", "Nova nota", "Nuova nota", "新しいノート", "新笔记"),
        SoundEvent.NoteArchived => T("Note archived", "Nota archivada", "Notiz archiviert", "Note archivée", "Nota arquivada", "Nota archiviata", "ノートをアーカイブしました", "笔记已归档"),
        SoundEvent.NoteOpened => T("Note opened", "Nota abierta", "Notiz geöffnet", "Note ouverte", "Nota aberta", "Nota aperta", "ノートを開きました", "笔记已打开"),
        SoundEvent.NoteClosed => T("Note closed", "Nota cerrada", "Notiz geschlossen", "Note fermée", "Nota fechada", "Nota chiusa", "ノートを閉じました", "笔记已关闭"),
        SoundEvent.TaskChecked => T("Task checked", "Casilla marcada", "Aufgabe abgehakt", "Case cochée", "Caixa marcada", "Attività completata", "タスクを完了しました", "任务已勾选"),
        SoundEvent.NoteDeleted => T("Note deleted", "Nota borrada", "Notiz gelöscht", "Note supprimée", "Nota excluída", "Nota eliminata", "ノートを削除しました", "笔记已删除"),
        SoundEvent.Typing => T("Typing", "Escribir", "Tippen", "Saisie", "Digitação", "Digitazione", "入力", "输入"),
        SoundEvent.TextDeleted => T("Deleting text", "Borrar texto", "Text löschen", "Effacement du texte", "Apagar texto", "Cancellazione del testo", "テキストの削除", "删除文字"),
        _ => "",
    };
    public static string SoundChoiceName(SoundChoice choice) => choice switch
    {
        SoundChoice.None => T("No sound", "Sin sonido", "Kein Ton", "Aucun son", "Sem som", "Nessun suono", "サウンドなし", "无声音"),
        SoundChoice.Asterisk => T("Windows: information", "Windows: información", "Windows: Information", "Windows : information", "Windows: informação", "Windows: informazione", "Windows: 情報", "Windows：信息"),
        SoundChoice.Exclamation => T("Windows: warning", "Windows: aviso", "Windows: Warnung", "Windows : avertissement", "Windows: aviso", "Windows: avviso", "Windows: 警告", "Windows：警告"),
        SoundChoice.Beep => T("Windows: beep", "Windows: pitido", "Windows: Signalton", "Windows : bip", "Windows: bipe", "Windows: segnale acustico", "Windows: ビープ音", "Windows：蜂鸣声"),
        SoundChoice.Hand => T("Windows: error", "Windows: error", "Windows: Fehler", "Windows : erreur", "Windows: erro", "Windows: errore", "Windows: エラー", "Windows：错误"),
        SoundChoice.Question => T("Windows: question", "Windows: pregunta", "Windows: Frage", "Windows : question", "Windows: pergunta", "Windows: domanda", "Windows: 質問", "Windows：询问"),
        SoundChoice.File => T("A .wav file…", "Un archivo .wav…", "Eine .wav-Datei…", "Un fichier .wav…", "Um arquivo .wav…", "Un file .wav…", ".wav ファイル…", ".wav 文件…"),
        SoundChoice.PageTurn => T("Aldune: turning a page", "Aldune: pasar hoja", "Aldune: Seite umblättern", "Aldune : tourner la page", "Aldune: virar a página", "Aldune: pagina che si volta", "Aldune: ページをめくる音", "Aldune：翻页声"),
        SoundChoice.PaperFold => T("Aldune: folding paper", "Aldune: papel que se pliega", "Aldune: Papier falten", "Aldune : papier qui se plie", "Aldune: papel dobrando", "Aldune: carta che si piega", "Aldune: 紙を折る音", "Aldune：折纸声"),
        SoundChoice.PenTap => T("Aldune: pen tap", "Aldune: toque de pluma", "Aldune: Federtupfer", "Aldune : touche de plume", "Aldune: toque de caneta", "Aldune: colpetto di penna", "Aldune: ペンで軽く叩く音", "Aldune：笔尖轻点声"),
        SoundChoice.PaperCrumple => T("Aldune: crumpling paper", "Aldune: papel que se arruga", "Aldune: Papier zerknüllen", "Aldune : papier froissé", "Aldune: papel amassando", "Aldune: carta accartocciata", "Aldune: 紙を丸める音", "Aldune：揉纸声"),
        SoundChoice.PenScratch => T("Aldune: pen scratch", "Aldune: rasguño de pluma", "Aldune: Federkratzen", "Aldune : grattement de plume", "Aldune: rabisco de caneta", "Aldune: graffio di penna", "Aldune: ペンで引っかく音", "Aldune：笔尖划过声"),
        SoundChoice.Eraser => T("Aldune: eraser", "Aldune: goma de borrar", "Aldune: Radiergummi", "Aldune : gomme", "Aldune: borracha", "Aldune: gomma", "Aldune: 消しゴム", "Aldune：橡皮擦声"),
        _ => "",
    };
    public static string SoundFileChoiceName(string name) => T($"File: {name}", $"Archivo: {name}", $"Datei: {name}", $"Fichier : {name}", $"Arquivo: {name}", $"File: {name}", $"ファイル: {name}", $"文件：{name}");
    // --- Copia automática -----------------------------------------------------------------------------
    public static string AutoExportCheckbox => T("Automatic copy of my notes in a folder", "Copia automática de mis notas en una carpeta", "Automatische Kopie meiner Notizen in einem Ordner", "Copie automatique de mes notes dans un dossier", "Cópia automática das minhas notas em uma pasta", "Copia automatica delle mie note in una cartella", "ノートをフォルダーに自動コピー", "自动将我的笔记复制到文件夹");
    public static string AutoExportHint => T(
        "Off by default. Writes one Markdown file per note a few seconds after each change and when you quit. Aldune only creates and rewrites its own files and never deletes anything: removed notes move to a _eliminadas folder, and a file you edited by hand is kept (the new copy is saved beside it). Notes with a password are left out. These files are NOT encrypted: choose the folder knowing that.",
        "Apagada de fábrica. Escribe un archivo Markdown por nota unos segundos después de cada cambio y al cerrar. Aldune solo crea y reescribe sus propios archivos y nunca borra nada: las notas eliminadas pasan a la carpeta _eliminadas, y un archivo que hayas editado a mano se respeta (la copia nueva se guarda a su lado). Las notas con contraseña quedan fuera. Estos archivos NO están cifrados: elige la carpeta sabiéndolo.",
        "Standardmäßig aus. Schreibt wenige Sekunden nach jeder Änderung und beim Beenden eine Markdown-Datei pro Notiz. Aldune erstellt und überschreibt nur eigene Dateien und löscht nie etwas: entfernte Notizen wandern in den Ordner _eliminadas, und eine von Hand bearbeitete Datei bleibt erhalten (die neue Kopie wird daneben gespeichert). Notizen mit Passwort bleiben außen vor. Diese Dateien sind NICHT verschlüsselt: Wähle den Ordner mit diesem Wissen.",
        "Désactivée par défaut. Écrit un fichier Markdown par note quelques secondes après chaque modification et à la fermeture. Aldune ne crée et ne réécrit que ses propres fichiers et ne supprime jamais rien : les notes supprimées vont dans le dossier _eliminadas, et un fichier modifié à la main est conservé (la nouvelle copie est enregistrée à côté). Les notes protégées par mot de passe sont exclues. Ces fichiers ne sont PAS chiffrés : choisissez le dossier en le sachant.",
        "Desligada por padrão. Grava um arquivo Markdown por nota alguns segundos após cada alteração e ao fechar. O Aldune só cria e reescreve os próprios arquivos e nunca apaga nada: as notas removidas vão para a pasta _eliminadas, e um arquivo editado à mão é preservado (a cópia nova é salva ao lado). Notas com senha ficam de fora. Esses arquivos NÃO são criptografados: escolha a pasta sabendo disso.", "Disattivata per impostazione predefinita. Scrive un file Markdown per nota pochi secondi dopo ogni modifica e quando esci. Aldune crea e riscrive solo i propri file e non cancella mai nulla: le note rimosse vengono spostate in una cartella _eliminadas e un file che hai modificato a mano viene conservato (la nuova copia viene salvata accanto). Le note con password sono escluse. Questi file NON sono cifrati: scegli la cartella tenendone conto.", "初期設定ではオフです。変更の数秒後と終了時に、ノートごとに 1 つの Markdown ファイルを書き出します。Aldune は自分のファイルの作成と上書きだけを行い、何も削除しません。削除したノートは _eliminadas フォルダーに移され、手動で編集したファイルは残されます（新しいコピーはその横に保存されます）。パスワード付きのノートは対象外です。これらのファイルは暗号化されていません。その点を理解したうえでフォルダーを選んでください。", "默认关闭。每次修改几秒后以及退出时，为每条笔记写出一个 Markdown 文件。Aldune 只创建和重写自己的文件，绝不删除任何内容：被移除的笔记会移到 _eliminadas 文件夹，您手动编辑过的文件会被保留（新副本保存在它旁边）。设有密码的笔记不会包含在内。这些文件没有加密：请在了解这一点的前提下选择文件夹。");
    public static string AutoExportChooseFolder => T("Choose folder…", "Elegir carpeta…", "Ordner wählen…", "Choisir un dossier…", "Escolher pasta…", "Scegli cartella…", "フォルダーを選択…", "选择文件夹…");
    public static string AutoExportNoFolder => T("No folder chosen", "Ninguna carpeta elegida", "Kein Ordner gewählt", "Aucun dossier choisi", "Nenhuma pasta escolhida", "Nessuna cartella scelta", "フォルダーが選択されていません", "未选择文件夹");
    public static string AutoExportArchivedCheckbox => T("Include archived notes", "Incluir las notas archivadas", "Archivierte Notizen einbeziehen", "Inclure les notes archivées", "Incluir as notas arquivadas", "Includi le note archiviate", "アーカイブしたノートを含める", "包含已归档的笔记");
    public static string AutoExportFailed(string error) => T($"The last copy failed: {error}", $"La última copia falló: {error}", $"Die letzte Kopie ist fehlgeschlagen: {error}", $"La dernière copie a échoué : {error}", $"A última cópia falhou: {error}", $"L'ultima copia non è riuscita: {error}", $"直前のコピーに失敗しました: {error}", $"上一次复制失败：{error}");
    public static string SoundVolumeLabel => T("Volume", "Volumen", "Lautstärke", "Volume", "Volume", "Volume", "音量", "音量");
    public static string SoundPaperAndPenButton => T("Use the suggested sounds", "Usar los sonidos sugeridos", "Empfohlene Töne verwenden", "Utiliser les sons suggérés", "Usar os sons sugeridos", "Usa i suoni suggeriti", "おすすめのサウンドを使う", "使用推荐的声音");
    public static string SoundTest => T("Test", "Probar", "Testen", "Tester", "Testar", "Prova", "テスト", "试听");
    public static string SoundFileFilter => T("WAV sound", "Sonido WAV", "WAV-Ton", "Son WAV", "Som WAV", "Suono WAV", "WAV サウンド", "WAV 声音") + " (*.wav)|*.wav";
    public static string SoundFileRejected => T(
        "Only .wav files up to 5 MB can be used as a sound.",
        "Solo se pueden usar como sonido archivos .wav de hasta 5 MB.",
        "Als Ton sind nur .wav-Dateien bis 5 MB möglich.",
        "Seuls les fichiers .wav jusqu'à 5 Mo peuvent servir de son.",
        "Só arquivos .wav de até 5 MB podem ser usados como som.", "Come suono si possono usare solo file .wav fino a 5 MB.", "サウンドに使えるのは 5 MB までの .wav ファイルだけです。", "只能使用不超过 5 MB 的 .wav 文件作为声音。");
    public static string ReminderSnooze10 => T("Snooze 10 min", "Posponer 10 min", "10 Min. später", "Reporter de 10 min", "Adiar 10 min", "Posticipa di 10 min", "10 分後に再通知", "10 分钟后再提醒");
    public static string ReminderSnooze60 => T("Snooze 1 h", "Posponer 1 h", "1 Std. später", "Reporter d'1 h", "Adiar 1 h", "Posticipa di 1 h", "1 時間後に再通知", "1 小时后再提醒");
    public static string ReminderOpenNote => T("Open note", "Abrir nota",
        "Notiz öffnen", "Ouvrir la note", "Abrir nota", "Apri nota", "ノートを開く", "打开笔记");
    public static string ReminderShowNotes => T("Show notes", "Ver notas",
        "Notizen anzeigen", "Afficher les notes", "Ver notas", "Mostra le note", "ノートを表示", "显示笔记");
    public static string WelcomeOpenManager => T("Show my notes", "Ver mis notas",
        "Meine Notizen anzeigen", "Afficher mes notes", "Ver minhas notas", "Mostra le mie note", "自分のノートを表示", "显示我的笔记");
    public static string ReminderPendingTasks(int pending, IReadOnlyList<string> first)
    {
        var list = string.Join(", ", first) + (pending > first.Count ? "…" : "");
        return T($"Still to do ({pending}): {list}", $"Falta ({pending}): {list}",
            $"Noch zu erledigen ({pending}): {list}",
            $"Reste à faire ({pending}) : {list}",
            $"Ainda por fazer ({pending}): {list}", $"Ancora da fare ({pending}): {list}", $"残り {pending} 件: {list}", $"还剩 {pending} 项：{list}");
    }
    public static string ReminderManyDue(int count) => T($"{count} reminders pending", $"{count} recordatorios pendientes",
        $"{count} Erinnerungen ausstehend", $"{count} rappels en attente", $"{count} lembretes pendentes", $"{count} promemoria in sospeso", $"{count} 件のリマインダーが保留中", $"{count} 个提醒待处理");
    public static string Cut => T("Cut", "Cortar",
        "Ausschneiden", "Couper", "Recortar", "Taglia", "切り取り", "剪切");
    public static string Copy => T("Copy", "Copiar",
        "Kopieren", "Copier", "Copiar", "Copia", "コピー", "复制");
    public static string Paste => T("Paste", "Pegar",
        "Einfügen", "Coller", "Colar", "Incolla", "貼り付け", "粘贴");
    public static string Archive => T("Archive", "Archivar",
        "Archivieren", "Archiver", "Arquivar", "Archivia", "アーカイブ", "归档");
    public static string Restore => T("Restore", "Restaurar",
        "Wiederherstellen", "Restaurer", "Restaurar", "Ripristina", "元に戻す", "恢复");
    public static string MoveToTrash => T("Move to trash", "Mover a la papelera",
        "In den Papierkorb verschieben", "Déplacer vers la corbeille", "Mover para a lixeira", "Sposta nel cestino", "ごみ箱に移動", "移到回收站");
    public static string Trash => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira", "Cestino", "ごみ箱", "回收站");
    public static string Archived => T("Archived", "Archivada",
        "Archiviert", "Archivée", "Arquivada", "Archiviate", "アーカイブ済み", "已归档");

    /// <summary>
    /// El título que muestran la pestaña del dock y la barra de tareas para una nota sin texto
    /// todavía. Se copia a <see cref="Aldune.Core.NoteTitleHelper.PlaceholderTitle"/> al arrancar
    /// (Core no depende de idiomas) — ver <c>App.OnStartup</c>.
    /// </summary>
    public static string NewNotePlaceholder => T("Untitled", "Sin título",
        "Ohne Titel", "Sans titre", "Sem título", "Senza titolo", "無題", "无标题");

    // --- Ventana de nota --------------------------------------------------------------------------

    public static string TitlePlaceholder => T("Title", "Título",
        "Titel", "Titre", "Título", "Titolo", "タイトル", "标题");
    public static string BodyPlaceholder => T("Write something…  ·  Ctrl+L for a task", "Escribe algo…  ·  Ctrl+L para una tarea",
        "Schreib etwas…  ·  Ctrl+L für eine Aufgabe",
        "Écrivez quelque chose…  ·  Ctrl+L pour une tâche",
        "Escreva algo…  ·  Ctrl+L para uma tarefa", "Scrivi qualcosa…  ·  Ctrl+L per un'attività", "何か書いてください…  ·  Ctrl+L でタスクに", "写点什么吧…  ·  Ctrl+L 转为任务");
    public static string MoreActionsTooltip => T("More actions", "Más acciones",
        "Weitere Aktionen", "Plus d'actions", "Mais ações", "Altre azioni", "その他の操作", "更多操作");
    public static string CloseTooltip => T("Close (Esc)", "Cerrar (Esc)",
        "Schließen (Esc)", "Fermer (Esc)", "Fechar (Esc)", "Chiudi (Esc)", "閉じる (Esc)", "关闭 (Esc)");
    public static string ConvertToTask => T("Convert to task", "Convertir en tarea",
        "In Aufgabe umwandeln", "Convertir en tâche", "Converter em tarefa", "Converti in attività", "タスクに変換", "转换为任务");
    public static string ConvertToBullet => T("Convert to list", "Convertir en lista",
        "In Liste umwandeln", "Convertir en liste", "Converter em liste", "Converti in elenco", "リストに変換", "转换为列表");
    public static string UncheckAllTasks => T("Uncheck all", "Desmarcar todas",
        "Alle abwählen", "Tout décocher", "Desmarcar todas", "Deseleziona tutto", "すべてのチェックを外す", "取消全部勾选");
    public static string RemoveCheckedTasks => T("Remove checked tasks", "Borrar las tareas hechas",
        "Erledigte Aufgaben entfernen", "Supprimer les tâches terminées", "Remover tarefas concluídas", "Rimuovi le attività completate", "完了したタスクを削除", "删除已勾选的任务");
    public static string AllTasksDone => T("✓ All done", "✓ Todo hecho",
        "✓ Alles erledigt", "✓ Tout est fait", "✓ Tudo pronto", "✓ Tutto fatto", "✓ すべて完了", "✓ 全部完成");
    public static string CustomColor => T("Choose another color…", "Elegir otro color…",
        "Andere Farbe wählen…", "Choisir une autre couleur…", "Escolher outra cor…", "Scegli un altro colore…", "別の色を選択…", "选择其他颜色…");
    public static string CustomColorContrastError => T(
        "Enter a valid HEX color.",
        "Escribe un color HEX válido.",
        "Gib eine gültige HEX-Farbe ein.", "Entrez une couleur HEX valide.", "Digite uma cor HEX válida.", "Inserisci un colore HEX valido.", "有効な HEX カラーを入力してください。", "请输入有效的 HEX 颜色。");
    public static string CustomColorWindowTitle => T("Custom note color", "Color personalizado de la nota",
        "Benutzerdefinierte Notizfarbe", "Couleur de note personnalisée", "Cor personalizada da nota", "Colore personalizzato della nota", "ノートのカスタムカラー", "自定义笔记颜色");
    public static string CustomColorWindowHint => T(
        "Text color adapts to keep your note readable.",
        "El color del texto se adapta para que la nota siga siendo legible.",
        "Die Textfarbe passt sich an, damit deine Notiz lesbar bleibt.",
        "La couleur du texte s'adapte pour que votre note reste lisible.",
        "A cor do texto se adapta para manter sua nota legível.", "Il colore del testo si adatta per mantenere leggibile la nota.", "ノートが読みやすいように、文字色は自動で調整されます。", "文字颜色会自动调整，以保证笔记清晰易读。");
    public static string CustomColorPreview => T("Preview", "Vista previa",
        "Vorschau", "Aperçu", "Visualização", "Anteprima", "プレビュー", "预览");
    public static string CustomColorSpectrumHint => T(
        "Pick a tone visually, then fine-tune it below.",
        "Elige un tono visualmente y ajústalo debajo.",
        "Wähle visuell einen Farbton und passe ihn unten an.",
        "Choisissez une nuance visuellement, puis ajustez-la ci-dessous.",
        "Escolha um tom visualmente e ajuste-o abaixo.", "Scegli un tono visivamente, poi rifiniscilo qui sotto.", "色合いを視覚的に選び、下で微調整します。", "先直观地挑选色调，再在下方微调。");
    public static string CustomColorHexLabel => T("HEX color", "Color HEX",
        "HEX-Farbe", "Couleur HEX", "Cor HEX", "Colore HEX", "HEX カラー", "HEX 颜色");
    public static string CustomColorRgbLabel => T("Fine tune with RGB", "Ajuste fino con RGB",
        "Feinabstimmung mit RGB", "Affiner avec RVB", "Ajuste fino com RGB", "Rifinisci con RGB", "RGB で微調整", "用 RGB 微调");
    public static string CustomColorReadable => T("Readable text", "Texto legible",
        "Lesbarer Text", "Texte lisible", "Texto legível", "Testo leggibile", "読みやすい文字", "清晰的文字");
    public static string CustomColorInvalidHex => T("Enter a color like #F5E3B3.", "Escribe un color como #F5E3B3.",
        "Gib eine Farbe wie #F5E3B3 ein.",
        "Entrez une couleur comme #F5E3B3.",
        "Digite uma cor como #F5E3B3.", "Inserisci un colore come #F5E3B3.", "#F5E3B3 のような色を入力してください。", "请输入类似 #F5E3B3 的颜色。");
    public static string DialogOk => T("OK", "Aceptar",
        "OK", "OK", "OK", "OK", "OK", "确定");
    public static string DialogCancel => T("Cancel", "Cancelar",
        "Abbrechen", "Annuler", "Cancelar", "Annulla", "キャンセル", "取消");
    public static string DialogYes => T("Yes", "Sí",
        "Ja", "Oui", "Sim", "Sì", "はい", "是");
    public static string DialogNo => T("No", "No",
        "Nein", "Non", "Não", "No", "いいえ", "否");
    public static string CustomColorCancel => T("Cancel", "Cancelar",
        "Abbrechen", "Annuler", "Cancelar", "Annulla", "キャンセル", "取消");
    public static string CustomColorApply => T("Use this color", "Usar este color",
        "Diese Farbe verwenden", "Utiliser cette couleur", "Usar esta cor", "Usa questo colore", "この色を使う", "使用此颜色");
    public static string RestoreSize => T("Restore size", "Restaurar tamaño",
        "Größe wiederherstellen", "Restaurer la taille", "Restaurar tamanho", "Ripristina dimensione", "サイズを元に戻す", "恢复大小");
    public static string ReturnToDock => T("Return next to the dock", "Volver junto al dock",
        "Zurück neben das Dock", "Remettre près du dock", "Voltar para junto do dock", "Torna accanto al dock", "ドックの横に戻す", "回到程序坞旁边");
    public static string ExportToMarkdown => T("Export to Markdown", "Exportar a Markdown",
        "Nach Markdown exportieren", "Exporter vers Markdown", "Exportar para Markdown", "Esporta in Markdown", "Markdown にエクスポート", "导出为 Markdown");
    public static string MarkdownFileFilter => T("Markdown file (*.md)|*.md", "Archivo Markdown (*.md)|*.md",
        "Markdown-Datei (*.md)|*.md", "Fichier Markdown (*.md)|*.md", "Arquivo Markdown (*.md)|*.md", "File Markdown (*.md)|*.md", "Markdown ファイル (*.md)|*.md", "Markdown 文件 (*.md)|*.md");

    // --- Actualizaciones ---------------------------------------------------------------------------

    public static string TrayCheckForUpdates => T("Check for updates…", "Buscar actualizaciones…",
        "Nach Updates suchen…", "Rechercher des mises à jour…", "Verificar atualizações…", "Cerca aggiornamenti…", "アップデートを確認…", "检查更新…");
    public static string CheckForUpdatesAutomaticallyCheckbox => T("Check for new versions automatically", "Buscar versiones nuevas automáticamente",
        "Automatisch nach neuen Versionen suchen",
        "Rechercher automatiquement les nouvelles versions",
        "Verificar novas versões automaticamente", "Cerca automaticamente nuove versioni", "新しいバージョンを自動的に確認する", "自动检查新版本");
    public static string CheckForUpdatesAutomaticallyHint => T("Once a day Aldune asks GitHub for the list of releases. Nothing about you or your notes is sent. Turned off, it only checks when you press the button.", "Una vez al día Aldune pide a GitHub la lista de versiones publicadas. No se envía nada tuyo ni de tus notas. Si lo desactivas, solo se comprueba al pulsar el botón.",
        "Einmal täglich fragt Aldune bei GitHub nach der Liste veröffentlichter Versionen. Es werden keine Daten über dich oder deine Notizen gesendet. Wenn deaktiviert, wird nur geprüft, wenn du auf die Schaltfläche klickst.",
        "Une fois par jour, Aldune demande à GitHub la liste des versions publiées. Rien sur vous ou vos notes n'est envoyé. Si désactivé, la vérification ne se fait que lorsque vous appuyez sur le bouton.",
        "Uma vez por dia o Aldune consulta o GitHub para obter a lista de versões. Nada sobre você ou suas notas é enviado. Quando desativado, só verifica ao clicar no botão.", "Una volta al giorno Aldune chiede a GitHub l'elenco delle versioni. Non viene inviato nulla su di te o sulle tue note. Se è disattivato, controlla solo quando premi il pulsante.", "1 日に 1 回、Aldune が GitHub にリリース一覧を問い合わせます。あなたやノートに関する情報は一切送信されません。オフの場合は、ボタンを押したときだけ確認します。", "Aldune 每天会向 GitHub 请求一次发布列表，不会发送任何关于您或您笔记的信息。关闭后，只有按下按钮时才会检查。");
    // --- Exportar e importar la configuración (Ajustes, Acerca de) -----------------------------------

    public static string ConfigSectionTitle => T("Configuration", "Configuración",
        "Konfiguration", "Configuration", "Configuração", "Configurazione", "設定ファイル", "配置");
    public static string ConfigAboutHint => T(
        "Save your appearance and settings to a file, or load them from one: to use them on another computer or to come back to them later.",
        "Guarda tu aspecto y tus ajustes en un archivo, o cárgalos desde uno: para usarlos en otro equipo o volver a ellos más adelante.",
        "Speichere Erscheinungsbild und Einstellungen in einer Datei oder lade sie aus einer Datei: für einen anderen Computer oder um später darauf zurückzukommen.",
        "Enregistrez votre apparence et vos paramètres dans un fichier, ou chargez-les depuis un fichier : pour les utiliser sur un autre ordinateur ou y revenir plus tard.",
        "Salve sua aparência e suas configurações em um arquivo, ou carregue-as de um arquivo: para usá-las em outro computador ou voltar a elas mais tarde.", "Salva aspetto e impostazioni in un file, oppure caricali da uno: per usarli su un altro computer o ritrovarli più avanti.", "外観と設定をファイルに保存したり、ファイルから読み込んだりします。別のパソコンで使う場合や、後で元に戻す場合に便利です。", "将您的外观和设置保存到文件，或从文件中载入：可在另一台电脑上使用，或日后恢复。");
    public static string ConfigExportButton => T("Export settings…", "Exportar configuración…",
        "Einstellungen exportieren…", "Exporter la configuration…", "Exportar configuração…", "Esporta impostazioni…", "設定をエクスポート…", "导出设置…");
    public static string ConfigImportButton => T("Import settings…", "Importar configuración…",
        "Einstellungen importieren…", "Importer la configuration…", "Importar configuração…", "Importa impostazioni…", "設定をインポート…", "导入设置…");
    public static string ConfigExportTitle => T("Export settings", "Exportar configuración",
        "Einstellungen exportieren", "Exporter la configuration", "Exportar configuração", "Esporta impostazioni", "設定のエクスポート", "导出设置");
    public static string ConfigExportHint => T(
        "Saves your choices to a file you can import on another computer. Sync settings, keys and passwords, the chosen screen and note positions are never included.",
        "Guarda tus preferencias en un archivo que puedes importar en otro equipo. La sincronización, las claves y contraseñas, la pantalla elegida y las posiciones de las notas nunca se incluyen.",
        "Speichert deine Einstellungen in einer Datei, die du auf einem anderen Computer importieren kannst. Synchronisierung, Schlüssel und Passwörter, der gewählte Bildschirm und die Notizpositionen werden nie exportiert.",
        "Enregistre vos préférences dans un fichier que vous pouvez importer sur un autre ordinateur. La synchronisation, les clés et mots de passe, l'écran choisi et la position des notes ne sont jamais inclus.",
        "Salva suas preferências em um arquivo que você pode importar em outro computador. A sincronização, as chaves e senhas, a tela escolhida e as posições das notas nunca são incluídas.", "Salva le tue scelte in un file che puoi importare su un altro computer. Le impostazioni di sincronizzazione, le chiavi e le password, lo schermo scelto e le posizioni delle note non sono mai incluse.", "選択内容を、別のパソコンでインポートできるファイルに保存します。同期の設定、キー、パスワード、選択した画面、ノートの位置は含まれません。", "将您的选择保存到一个文件中，可在另一台电脑上导入。同步设置、密钥和密码、所选屏幕以及笔记位置不会包含在内。");
    public static string ConfigSectionAppearance => T("Appearance", "Aspecto",
        "Erscheinungsbild", "Apparence", "Aparência", "Aspetto", "外観", "外观");
    public static string ConfigSectionAppearanceHint => T(
        "Look, colors, corners, sync signal, note theme and your own themes.",
        "Aspecto, colores, esquinas, señal de sincronización, tema de notas y temas propios.",
        "Aussehen, Farben, Ecken, Sync-Signal, Notizthema und eigene Themen.",
        "Apparence, couleurs, coins, signal de synchronisation, thème des notes et thèmes personnalisés.",
        "Aparência, cores, cantos, sinal de sincronização, tema das notas e temas próprios.", "Stile, colori, angoli, segnale di sincronizzazione, tema delle note e temi personalizzati.", "デザイン、色、角、同期の表示、ノートのテーマ、独自のテーマ。", "样式、颜色、边角、同步指示、笔记主题和您自己的主题。");
    public static string ConfigSectionSettings => T("Settings", "Ajustes",
        "Einstellungen", "Paramètres", "Configurações", "Impostazioni", "設定", "设置");
    public static string ConfigSectionSettingsHint => T(
        "Language, shortcuts, dock, tasks, trash and more.",
        "Idioma, atajos, dock, tareas, papelera y más.",
        "Sprache, Tastenkürzel, Dock, Aufgaben, Papierkorb und mehr.",
        "Langue, raccourcis, dock, tâches, corbeille et plus.",
        "Idioma, atalhos, dock, tarefas, lixeira e mais.", "Lingua, scorciatoie, dock, attività, cestino e altro.", "言語、ショートカット、ドック、タスク、ごみ箱など。", "语言、快捷键、程序坞、任务、回收站等。");
    public static string ConfigExportSave => T("Export", "Exportar",
        "Exportieren", "Exporter", "Exportar", "Esporta", "エクスポート", "导出");
    public static string ConfigExportFailed => T(
        "Couldn't save the file. Check that the location can be written to.",
        "No se pudo guardar el archivo. Comprueba que se puede escribir en esa ubicación.",
        "Die Datei konnte nicht gespeichert werden. Prüfe, ob an diesem Ort geschrieben werden darf.",
        "Impossible d'enregistrer le fichier. Vérifiez que l'on peut écrire à cet emplacement.",
        "Não foi possível salvar o arquivo. Verifique se é possível gravar nesse local.", "Impossibile salvare il file. Controlla che nella posizione si possa scrivere.", "ファイルを保存できませんでした。保存先に書き込めることを確認してください。", "无法保存文件。请检查该位置是否可写入。");
    public static string ConfigImportTitle => T("Import settings", "Importar configuración",
        "Einstellungen importieren", "Importer la configuration", "Importar configuração", "Importa impostazioni", "設定のインポート", "导入设置");
    public static string ConfigImportNoChanges => T(
        "The file has nothing different from your current settings.",
        "El archivo no trae nada distinto de tu configuración actual.",
        "Die Datei enthält nichts, was von deinen aktuellen Einstellungen abweicht.",
        "Le fichier ne contient rien de différent de votre configuration actuelle.",
        "O arquivo não traz nada diferente da sua configuração atual.", "Il file non contiene nulla di diverso dalle tue impostazioni attuali.", "このファイルには、現在の設定と異なる項目がありません。", "此文件与您当前的设置没有任何不同。");
    public static string ConfigImportSummary(int count) => count == 1
        ? T("This file changes 1 setting:", "Este archivo cambia 1 ajuste:",
            "Diese Datei ändert 1 Einstellung:", "Ce fichier modifie 1 paramètre :", "Este arquivo altera 1 configuração:", "Questo file modifica 1 impostazione:", "このファイルは 1 件の設定を変更します:", "此文件将更改 1 项设置：")
        : T($"This file changes {count} settings:", $"Este archivo cambia {count} ajustes:",
            $"Diese Datei ändert {count} Einstellungen:", $"Ce fichier modifie {count} paramètres :",
            $"Este arquivo altera {count} configurações:", $"Questo file modifica {count} impostazioni:", $"このファイルは {count} 件の設定を変更します:", $"此文件将更改 {count} 项设置：");
    public static string ConfigImportApply => T("Apply", "Aplicar",
        "Anwenden", "Appliquer", "Aplicar", "Applica", "適用", "应用");
    public static string ConfigImportClose => T("Close", "Cerrar",
        "Schließen", "Fermer", "Fechar", "Chiudi", "閉じる", "关闭");
    public static string ConfigImportRestartForLanguage => T(
        "The language changes when you restart Aldune.",
        "El idioma cambia al reiniciar Aldune.",
        "Die Sprache ändert sich beim Neustart von Aldune.",
        "La langue change au redémarrage d'Aldune.",
        "O idioma muda ao reiniciar o Aldune.", "La lingua cambia al riavvio di Aldune.", "言語の変更は Aldune を再起動すると反映されます。", "语言将在您重新启动 Aldune 后更改。");
    public static string ConfigImportBackupNote => T(
        "A copy of your current settings is saved first.",
        "Antes se guarda una copia de tu configuración actual.",
        "Vorher wird eine Kopie deiner aktuellen Einstellungen gespeichert.",
        "Une copie de votre configuration actuelle est d'abord enregistrée.",
        "Antes, uma cópia da sua configuração atual é salva.", "Prima viene salvata una copia delle tue impostazioni attuali.", "先に現在の設定のコピーを保存します。", "会先保存一份您当前设置的副本。");
    public static string ConfigImportFromVersion(string app) => T(
        $"File from Aldune {app}", $"Archivo de Aldune {app}",
        $"Datei aus Aldune {app}", $"Fichier d'Aldune {app}", $"Arquivo do Aldune {app}", $"File di Aldune {app}", $"Aldune {app} のファイル", $"来自 Aldune {app} 的文件");
    // Genérico a propósito: el detalle que da Core va en español fijo y no es para enseñarlo.
    public static string ConfigImportInvalidFile => T(
        "Couldn't read the file: it isn't a valid Aldune settings file.",
        "No se pudo leer el archivo: no es un archivo de configuración de Aldune válido.",
        "Die Datei konnte nicht gelesen werden: Es ist keine gültige Aldune-Konfigurationsdatei.",
        "Impossible de lire le fichier : ce n'est pas un fichier de configuration Aldune valide.",
        "Não foi possível ler o arquivo: não é um arquivo de configuração do Aldune válido.", "Impossibile leggere il file: non è un file di impostazioni di Aldune valido.", "ファイルを読み込めませんでした。有効な Aldune の設定ファイルではありません。", "无法读取该文件：它不是有效的 Aldune 设置文件。");
    public static string ConfigImportUnreadable => T(
        "Couldn't open the file. Check that it exists and that you have permission to read it.",
        "No se pudo abrir el archivo. Comprueba que existe y que tienes permiso para leerlo.",
        "Die Datei konnte nicht geöffnet werden. Prüfe, ob sie existiert und du sie lesen darfst.",
        "Impossible d'ouvrir le fichier. Vérifiez qu'il existe et que vous avez le droit de le lire.",
        "Não foi possível abrir o arquivo. Verifique se ele existe e se você tem permissão para lê-lo.", "Impossibile aprire il file. Controlla che esista e di avere il permesso di leggerlo.", "ファイルを開けませんでした。ファイルが存在し、読み取りの権限があることを確認してください。", "无法打开该文件。请检查文件是否存在以及您是否有读取权限。");
    public static string ConfigImportBackupFailed => T(
        "Couldn't save a copy of your current settings, so nothing was imported.",
        "No se pudo guardar una copia de tu configuración actual, así que no se ha importado nada.",
        "Es konnte keine Kopie deiner aktuellen Einstellungen gespeichert werden, daher wurde nichts importiert.",
        "Impossible d'enregistrer une copie de votre configuration actuelle : rien n'a été importé.",
        "Não foi possível salvar uma cópia da sua configuração atual, então nada foi importado.", "Impossibile salvare una copia delle tue impostazioni attuali, quindi non è stato importato nulla.", "現在の設定のコピーを保存できなかったため、何もインポートされませんでした。", "无法保存当前设置的副本，因此没有导入任何内容。");
    public static string ConfigImportSaveFailed => T(
        "The settings were applied but couldn't be saved, so they will be lost when Aldune closes.",
        "Los ajustes se aplicaron pero no se pudieron guardar, así que se perderán al cerrar Aldune.",
        "Die Einstellungen wurden angewendet, konnten aber nicht gespeichert werden und gehen beim Beenden von Aldune verloren.",
        "Les paramètres ont été appliqués mais n'ont pas pu être enregistrés : ils seront perdus à la fermeture d'Aldune.",
        "As configurações foram aplicadas, mas não puderam ser salvas, então serão perdidas ao fechar o Aldune.", "Le impostazioni sono state applicate ma non è stato possibile salvarle, quindi andranno perse alla chiusura di Aldune.", "設定は適用されましたが保存できなかったため、Aldune を閉じると失われます。", "设置已应用，但无法保存，因此在 Aldune 关闭后会丢失。");
    public static string ConfigValueOn => T("On", "Activado", "Ein", "Activé", "Ativado", "Sì", "オン", "开");
    public static string ConfigValueOff => T("Off", "Desactivado", "Aus", "Désactivé", "Desativado", "No", "オフ", "关");
    // Una sola fila por atajo en la vista previa (modificadores y tecla juntos).
    public static string ConfigFieldHotkeyCombo => T("New note shortcut (combination)", "Atajo de nota nueva (combinación)",
        "Tastenkürzel für neue Notiz (Kombination)", "Raccourci de nouvelle note (combinaison)", "Atalho de nova nota (combinação)", "Scorciatoia nuova nota (combinazione)", "新しいノートのショートカット（組み合わせ）", "新建笔记快捷键（组合键）");
    public static string ConfigFieldRecentHotkeyCombo => T("Last note shortcut (combination)", "Atajo de la última nota (combinación)",
        "Tastenkürzel für letzte Notiz (Kombination)", "Raccourci de la dernière note (combinaison)", "Atalho da última nota (combinação)", "Scorciatoia ultima nota (combinazione)", "最後のノートのショートカット（組み合わせ）", "最近笔记快捷键（组合键）");
    public static string ConfigFieldManagerHotkeyCombo => T("Open notes shortcut (combination)", "Atajo de abrir las notas (combinación)",
        "Tastenkürzel zum Öffnen der Notizen (Kombination)", "Raccourci d'ouverture des notes (combinaison)", "Atalho para abrir as notas (combinação)", "Scorciatoia apri note (combinazione)", "ノート一覧を開くショートカット（組み合わせ）", "打开笔记快捷键（组合键）");
    public static string ConfigImportDone => T("Settings imported.", "Configuración importada.",
        "Einstellungen importiert.", "Configuration importée.", "Configuração importada.", "Impostazioni importate.", "設定をインポートしました。", "设置已导入。");

    /// <summary>Nombre visible de cada campo del archivo de configuración (el id es la clave del JSON,
    /// ver ConfigFields). Un id desconocido se devuelve tal cual. Los de atajo llevan además la parte
    /// (modificadores / tecla) para que dos filas seguidas no se llamen igual.</summary>
    public static string ConfigFieldName(string id) => id switch
    {
        "appearance" => T("Appearance", "Aspecto", "Erscheinungsbild", "Apparence", "Aparência", "Aspetto", "外観", "外观"),
        "aspectColors" => T("Aspect colors", "Colores del aspecto", "Farben des Erscheinungsbilds", "Couleurs de l'apparence", "Cores da aparência", "Colori dell'aspetto", "デザインの色", "外观颜色"),
        "squareCorners" => T("Square corners", "Esquinas rectas", "Eckige Ecken", "Coins carrés", "Cantos retos", "Angoli squadrati", "角を四角にする", "方形边角"),
        "syncSignal" => T("Sync signal", "Señal de sincronización", "Sync-Signal", "Signal de synchronisation", "Sinal de sincronização", "Segnale di sincronizzazione", "同期の表示", "同步指示"),
        "uniformNoteColor" => T("Same color for every note", "Mismo color para todas las notas", "Gleiche Farbe für alle Notizen", "Même couleur pour toutes les notes", "Mesma cor para todas as notas", "Stesso colore per tutte le note", "すべてのノートを同じ色にする", "所有笔记使用同一颜色"),
        "noteTheme" => T("Note theme", "Tema de notas", "Notizthema", "Thème des notes", "Tema das notas", "Tema delle note", "ノートのテーマ", "笔记主题"),
        "customThemes" => T("Custom themes", "Temas propios", "Eigene Themen", "Thèmes personnalisés", "Temas próprios", "Temi personalizzati", "カスタムテーマ", "自定义主题"),
        "newNoteTone" => T("New note tone", "Tono de las notas nuevas", "Farbton neuer Notizen", "Ton des nouvelles notes", "Tom das notas novas", "Tono delle nuove note", "新しいノートのトーン", "新笔记色调"),
        "colorAssignment" => T("Color for new notes", "Color de las notas nuevas", "Farbe neuer Notizen", "Couleur des nouvelles notes", "Cor das notas novas", "Colore delle nuove note", "新しいノートの色", "新笔记的颜色"),
        "fixedNoteColor" => T("Fixed note color", "Color fijo de las notas", "Feste Notizfarbe", "Couleur fixe des notes", "Cor fixa das notas", "Colore fisso della nota", "ノートの固定色", "固定笔记颜色"),
        "language" => T("Language", "Idioma", "Sprache", "Langue", "Idioma", "Lingua", "言語", "语言"),
        "simplifiedMode" => T("Simple interface", "Interfaz simple", "Einfache Oberfläche", "Interface simple", "Interface simples", "Interfaccia semplice", "シンプルなインターフェース", "简洁界面"),
        "hotkeyEnabled" => T("New note shortcut", "Atajo de nota nueva", "Tastenkürzel für neue Notiz", "Raccourci de nouvelle note", "Atalho de nova nota", "Scorciatoia nuova nota", "新しいノートのショートカット", "新建笔记快捷键"),
        "hotkeyModifiers" => T("New note shortcut (modifier keys)", "Atajo de nota nueva (modificadores)", "Tastenkürzel für neue Notiz (Zusatztasten)", "Raccourci de nouvelle note (touches modificatrices)", "Atalho de nova nota (teclas modificadoras)", "Scorciatoia nuova nota (tasti modificatori)", "新しいノートのショートカット（修飾キー）", "新建笔记快捷键（修饰键）"),
        "hotkeyKey" => T("New note shortcut (key)", "Atajo de nota nueva (tecla)", "Tastenkürzel für neue Notiz (Taste)", "Raccourci de nouvelle note (touche)", "Atalho de nova nota (tecla)", "Scorciatoia nuova nota (tasto)", "新しいノートのショートカット（キー）", "新建笔记快捷键（按键）"),
        "recentHotkeyEnabled" => T("Last note shortcut", "Atajo de la última nota", "Tastenkürzel für letzte Notiz", "Raccourci de la dernière note", "Atalho da última nota", "Scorciatoia ultima nota", "最後のノートのショートカット", "最近笔记快捷键"),
        "recentHotkeyModifiers" => T("Last note shortcut (modifier keys)", "Atajo de la última nota (modificadores)", "Tastenkürzel für letzte Notiz (Zusatztasten)", "Raccourci de la dernière note (touches modificatrices)", "Atalho da última nota (teclas modificadoras)", "Scorciatoia ultima nota (tasti modificatori)", "最後のノートのショートカット（修飾キー）", "最近笔记快捷键（修饰键）"),
        "recentHotkeyKey" => T("Last note shortcut (key)", "Atajo de la última nota (tecla)", "Tastenkürzel für letzte Notiz (Taste)", "Raccourci de la dernière note (touche)", "Atalho da última nota (tecla)", "Scorciatoia ultima nota (tasto)", "最後のノートのショートカット（キー）", "最近笔记快捷键（按键）"),
        "managerHotkeyEnabled" => T("Open notes shortcut", "Atajo de abrir las notas", "Tastenkürzel zum Öffnen der Notizen", "Raccourci d'ouverture des notes", "Atalho para abrir as notas", "Scorciatoia apri note", "ノート一覧を開くショートカット", "打开笔记快捷键"),
        "managerHotkeyModifiers" => T("Open notes shortcut (modifier keys)", "Atajo de abrir las notas (modificadores)", "Tastenkürzel zum Öffnen der Notizen (Zusatztasten)", "Raccourci d'ouverture des notes (touches modificatrices)", "Atalho para abrir as notas (teclas modificadoras)", "Scorciatoia apri note (tasti modificatori)", "ノート一覧を開くショートカット（修飾キー）", "打开笔记快捷键（修饰键）"),
        "managerHotkeyKey" => T("Open notes shortcut (key)", "Atajo de abrir las notas (tecla)", "Tastenkürzel zum Öffnen der Notizen (Taste)", "Raccourci d'ouverture des notes (touche)", "Atalho para abrir as notas (tecla)", "Scorciatoia apri note (tasto)", "ノート一覧を開くショートカット（キー）", "打开笔记快捷键（按键）"),
        "textScale" => T("Text size", "Tamaño del texto", "Textgröße", "Taille du texte", "Tamanho do texto", "Dimensione del testo", "文字サイズ", "文字大小"),
        "dockEdge" => T("Dock edge", "Borde del dock", "Dock-Rand", "Bord du dock", "Borda do dock", "Bordo del dock", "ドックの端", "程序坞边缘"),
        "dockView" => T("Dock view", "Vista del dock", "Dock-Ansicht", "Vue du dock", "Visualização do dock", "Vista del dock", "ドックの表示", "程序坞视图"),
        "keepDockOpen" => T("Keep dock open", "Mantener el dock abierto", "Dock geöffnet lassen", "Garder le dock ouvert", "Manter o dock aberto", "Mantieni il dock aperto", "ドックを開いたままにする", "保持程序坞展开"),
        "showNotePreview" => T("Note preview", "Vista previa de notas", "Notizvorschau", "Aperçu des notes", "Pré-visualização das notas", "Anteprima delle note", "ノートのプレビュー", "笔记预览"),
        "hideOnFullscreen" => T("Hide in fullscreen", "Ocultar a pantalla completa", "Im Vollbild ausblenden", "Masquer en plein écran", "Ocultar em tela cheia", "Nascondi a schermo intero", "全画面表示中は隠す", "全屏时隐藏"),
        "trackpadGestures" => T("Trackpad gestures", "Gestos del trackpad", "Trackpad-Gesten", "Gestes du pavé tactile", "Gestos do trackpad", "Gesti del trackpad", "トラックパッドのジェスチャー", "触控板手势"),
        "moveCompletedTasksToEnd" => T("Move completed tasks to the end", "Mover las tareas completadas al final", "Erledigte Aufgaben ans Ende verschieben", "Déplacer les tâches terminées à la fin", "Mover as tarefas concluídas para o fim", "Sposta le attività completate in fondo", "完了したタスクを末尾に移動", "将已完成的任务移到末尾"),
        "autoHideCompletedTasks" => T("Hide completed tasks automatically", "Ocultar tareas completadas automáticamente", "Erledigte Aufgaben automatisch ausblenden", "Masquer automatiquement les tâches terminées", "Ocultar tarefas concluídas automaticamente", "Nascondi automaticamente le attività completate", "完了したタスクを自動的に隠す", "自动隐藏已完成的任务"),
        "autoHideDelayValue" => T("Hide completed tasks after (amount)", "Ocultar completadas tras (cantidad)", "Erledigte nach (Anzahl) ausblenden", "Masquer les terminées après (quantité)", "Ocultar concluídas após (quantidade)", "Nascondi le attività completate dopo (quantità)", "完了したタスクを隠すまでの時間（数値）", "隐藏已完成任务的时间（数值）"),
        "autoHideDelayUnit" => T("Hide completed tasks after (unit)", "Ocultar completadas tras (unidad)", "Erledigte nach (Einheit) ausblenden", "Masquer les terminées après (unité)", "Ocultar concluídas após (unidade)", "Nascondi le attività completate dopo (unità)", "完了したタスクを隠すまでの時間（単位）", "隐藏已完成任务的时间（单位）"),
        "trashRetentionDays" => T("Days in the trash", "Días en la papelera", "Tage im Papierkorb", "Jours dans la corbeille", "Dias na lixeira", "Giorni nel cestino", "ごみ箱に残す日数", "在回收站中保留的天数"),
        "rememberNotePositions" => T("Remember note positions", "Recordar la posición de las notas", "Notizpositionen merken", "Mémoriser la position des notes", "Lembrar a posição das notas", "Ricorda le posizioni delle note", "ノートの位置を記憶する", "记住笔记位置"),
        "checkForUpdates" => T("Check for updates automatically", "Buscar actualizaciones automáticamente", "Automatisch nach Updates suchen", "Rechercher les mises à jour automatiquement", "Verificar atualizações automaticamente", "Cerca aggiornamenti automaticamente", "アップデートを自動的に確認する", "自动检查更新"),
        "reminderIncludesTasks" => T("Pending tasks in reminders", "Tareas pendientes en los recordatorios", "Offene Aufgaben in Erinnerungen", "Tâches en attente dans les rappels", "Tarefas pendentes nos lembretes", "Attività in sospeso nei promemoria", "リマインダーの未完了タスク", "提醒中的待办任务"),
        _ => id,
    };

    public static string UpdateChecking => T("Checking for updates…", "Buscando actualizaciones…",
        "Nach Updates wird gesucht…", "Recherche de mises à jour…", "Verificando atualizações…", "Ricerca degli aggiornamenti…", "アップデートを確認しています…", "正在检查更新…");
    public static string UpdateCurrentMessage => T("Aldune is up to date.", "Aldune está actualizado.",
        "Aldune ist auf dem neuesten Stand.", "Aldune est à jour.", "Aldune está atualizado.", "Aldune è aggiornato.", "Aldune は最新です。", "Aldune 已是最新版本。");
    public static string UpdateErrorMessage => T(
        "The update check failed. Try again later.",
        "No se ha podido comprobar si hay actualizaciones. Inténtalo más tarde.",
        "Die Update-Prüfung ist fehlgeschlagen. Versuche es später erneut.",
        "Échec de la recherche de mises à jour. Réessayez plus tard.",
        "Não foi possível verificar atualizações. Tente novamente mais tarde.", "Il controllo degli aggiornamenti non è riuscito. Riprova più tardi.", "アップデートの確認に失敗しました。後でもう一度お試しください。", "检查更新失败。请稍后再试。");
    public static string UpdateAvailableMessage(string version) =>
        T($"Aldune {version} is available.",
          $"Aldune {version} está disponible.",
            $"Aldune {version} ist verfügbar.",
            $"Aldune {version} est disponible.",
            $"Aldune {version} está disponível.", $"È disponibile Aldune {version}.", $"Aldune {version} が利用できます。", $"Aldune {version} 现已推出。");
    public static string UpdateAvailableTitle => T("Update available", "Actualización disponible",
        "Update verfügbar", "Mise à jour disponible", "Atualização disponível", "Aggiornamento disponibile", "アップデートがあります", "有可用更新");
    public static string UpdateDownload => T("Download", "Descargar",
        "Herunterladen", "Télécharger", "Baixar", "Scarica", "ダウンロード", "下载");
    public static string UpdateLater => T("Later", "Más tarde",
        "Später", "Plus tard", "Mais tarde", "Più tardi", "後で", "稍后");
    public static string UpdateWindowTitle => T("Aldune update", "Actualización de Aldune",
        "Aldune-Update", "Mise à jour d'Aldune", "Atualização do Aldune", "Aggiornamento di Aldune", "Aldune のアップデート", "Aldune 更新");
    public static string UpdateOpenErrorMessage => T(
        "The download page couldn't be opened.",
        "No se ha podido abrir la página de descarga.",
        "Die Download-Seite konnte nicht geöffnet werden.",
        "Impossible d'ouvrir la page de téléchargement.",
        "Não foi possível abrir a página de download.", "Impossibile aprire la pagina di download.", "ダウンロードページを開けませんでした。", "无法打开下载页面。");
    public static string ReminderMenuEntry => T("Reminder", "Recordatorio",
        "Erinnerung", "Rappel", "Lembrete", "Promemoria", "リマインダー", "提醒");
    public static string ReminderSet(DateTimeOffset dueAt) =>
        T($"Reminder: {dueAt:dd/MM HH:mm}", $"Recordatorio: {dueAt:dd/MM HH:mm}",
            $"Erinnerung: {dueAt:dd/MM HH:mm}",
            $"Rappel : {dueAt:dd/MM HH:mm}",
            $"Lembrete: {dueAt:dd/MM HH:mm}", $"Promemoria: {dueAt:dd/MM HH:mm}", $"リマインダー: {dueAt:dd/MM HH:mm}", $"提醒：{dueAt:dd/MM HH:mm}");
    public static string ReminderInOneHour => T("In 1 hour", "En 1 hora",
        "In 1 Stunde", "Dans 1 heure", "Em 1 hora", "Tra 1 ora", "1 時間後", "1 小时后");
    public static string ReminderTonight => T("Tonight", "Esta noche",
        "Heute Abend", "Ce soir", "Hoje à noite", "Stasera", "今夜", "今晚");
    public static string ReminderTomorrowMorning => T("Tomorrow 9:00", "Mañana 9:00",
        "Morgen 9:00", "Demain 9:00", "Amanhã às 9:00", "Domani alle 9:00", "明日 9:00", "明天 9:00");
    public static string ReminderSave => T("Save", "Guardar",
        "Speichern", "Enregistrer", "Salvar", "Salva", "保存", "保存");
    public static string ReminderClear => T("Remove reminder", "Quitar recordatorio",
        "Erinnerung entfernen", "Supprimer le rappel", "Remover lembrete", "Rimuovi promemoria", "リマインダーを削除", "移除提醒");
    public static string PinnedOn => T("✓  Always on top", "✓  Siempre encima",
        "✓  Immer im Vordergrund", "✓  Toujours au premier plan", "✓  Sempre no topo", "✓  Sempre in primo piano", "✓  常に手前に表示", "✓  始终置顶");
    public static string PinnedOff => T("Always on top", "Siempre encima",
        "Immer im Vordergrund", "Toujours au premier plan", "Sempre no topo", "Sempre in primo piano", "常に手前に表示", "始终置顶");
    public static string PinnedOnHint => T("The note stays in front of other windows.", "La nota se queda por delante de las demás ventanas.",
        "Die Notiz bleibt vor anderen Fenstern.",
        "La note reste devant les autres fenêtres.",
        "A nota fica à frente de outras janelas.", "La nota resta davanti alle altre finestre.", "ノートを他のウィンドウの前面に保ちます。", "笔记始终显示在其他窗口前面。");
    public static string PinnedOffHint => T("The note goes behind when you click another window.", "La nota se queda detrás al pinchar en otra ventana.",
        "Die Notiz tritt in den Hintergrund, wenn du auf ein anderes Fenster klickst.",
        "La note passe à l'arrière-plan lorsque vous cliquez sur une autre fenêtre.",
        "A nota vai para trás quando você clica em outra janela.", "La nota passa dietro quando fai clic su un'altra finestra.", "別のウィンドウをクリックすると、ノートが背面に移ります。", "点击其他窗口时，笔记会移到后面。");

    // --- Dock (mazo anclado al borde) -------------------------------------------------------------

    public static string ManageNotesTooltip => T("Manage notes", "Gestionar notas",
        "Notizen verwalten", "Gérer les notes", "Gerenciar notas", "Gestisci le note", "ノートを管理", "管理笔记");
    public static string ManageNotesTaggedTooltip(string tag) => T(
        $"Manage the notes with the tag \"{tag}\"", $"Gestionar las notas con la etiqueta \"{tag}\"",
        $"Notizen mit dem Tag \"{tag}\" verwalten",
        $"Gérer les notes avec l'étiquette \"{tag}\"",
        $"Gerenciar as notas com a etiqueta \"{tag}\"", $"Gestisci le note con l'etichetta \"{tag}\"", $"タグ「{tag}」のノートを管理", $"管理带有标签“{tag}”的笔记");
    public static string OpenAllNotesTooltip => T("Open all notes (closes them all if they're already open)", "Abrir todas las notas (las cierra todas si ya están abiertas)",
        "Alle Notizen öffnen (schließt alle, wenn sie bereits geöffnet sind)",
        "Ouvrir toutes les notes (les ferme toutes si elles sont déjà ouvertes)",
        "Abrir todas as notas (fecha todas se já estiverem abertas)", "Apri tutte le note (le chiude tutte se sono già aperte)", "すべてのノートを開く（すでに開いている場合はすべて閉じる）", "打开所有笔记（如果已全部打开，则全部关闭）");
    public static string OpenAllMenuTitle => T("Open all with...", "Abrir todas con...",
        "Alle öffnen mit...", "Tout ouvrir avec...", "Abrir todas com...", "Apri tutte con...", "次の方法ですべて開く...", "打开全部笔记的方式...");
    public static string OpenAllModeTitle => T("Opening mode", "Modo de apertura",
        "Öffnungsmodus", "Mode d'ouverture", "Modo de abertura", "Modalità di apertura", "開き方", "打开方式");
    public static string OpenAllMonitorTitle => T("Open notes on", "Abrir las notas en",
        "Notizen öffnen auf", "Ouvrir les notes sur", "Abrir notas em", "Apri le note su", "ノートを開く画面", "在以下位置打开笔记");
    public static string CloseAllNotes => T("Close all notes", "Cerrar todas las notas",
        "Alle Notizen schließen", "Fermer toutes les notes", "Fechar todas as notas", "Chiudi tutte le note", "すべてのノートを閉じる", "关闭所有笔记");
    public static string OpenAllGrid => T("Grid", "Cuadrícula",
        "Raster", "Grille", "Grade", "Griglia", "グリッド", "网格");
    public static string OpenAllColumns => T("Columns", "Columnas",
        "Spalten", "Colonnes", "Colunas", "Colonne", "列", "列");
    public static string CascadeNearDock => T("Cascade by the dock", "Cascada junto al dock",
        "Kaskadierend am Dock", "En cascade près du dock", "Cascata junto ao dock", "A cascata accanto al dock", "ドックの横にカスケード表示", "在程序坞旁层叠");
    public static string TrayToggleDock => T("Hide the dock", "Ocultar el dock",
        "Dock ausblenden", "Masquer le dock", "Ocultar o dock", "Nascondi il dock", "ドックを隠す", "隐藏程序坞");
    public static string TrayShowDock => T("Show the dock", "Mostrar el dock",
        "Dock einblenden", "Afficher le dock", "Mostrar o dock", "Mostra il dock", "ドックを表示", "显示程序坞");
    public static string TrackpadGesturesCheckbox => T("Trackpad gestures",
        "Gestos de trackpad",
        "Touchpad-Gesten", "Gestes du pavé tactile", "Gestos do touchpad", "Gesti del trackpad", "トラックパッドのジェスチャー", "触控板手势");
    public static string TrackpadGesturesHint => T(
        "Scrolls with the trackpad are read as continuous gestures instead of mouse notches, so two fingers move the list in proportion instead of jumping. Only available when a precision trackpad is detected.",
        "Los deslizamientos del trackpad se leen como gestos continuos en vez de muescas de ratón, así que dos dedos mueven la lista en proporción en vez de a saltos. Solo está disponible cuando se detecta un trackpad de precisión.",
        "Das Scrollen mit dem Touchpad wird als kontinuierliche Geste statt in Mausrad-Schritten erfasst, sodass zwei Finger die Liste gleichmäßig statt sprunghaft bewegen. Nur verfügbar, wenn ein Präzisions-Touchpad erkannt wird.",
        "Les défilements au pavé tactile sont interprétés comme des gestes continus et non des crans de souris, de sorte que deux doigts déplacent la liste proportionnellement sans sauts. Disponible uniquement lorsqu'un pavé tactile de précision est détecté.",
        "A rolagem com o touchpad é lida como gestos contínuos em vez de etapas do mouse, permitindo que dois dedos movam a lista suavemente em vez de saltos. Disponível apenas quando um touchpad de precisão for detectado.", "Lo scorrimento con il trackpad viene letto come gesto continuo invece che a scatti del mouse, così due dita muovono l'elenco in modo proporzionale senza saltare. Disponibile solo se viene rilevato un trackpad di precisione.", "トラックパッドのスクロールを、マウスホイールの段階的な操作ではなく連続したジェスチャーとして扱います。2 本の指でリストが飛ばずになめらかに動きます。高精度トラックパッドが検出された場合のみ利用できます。", "将触控板的滚动视为连续手势，而不是鼠标滚轮的一格一格，这样双指滑动时列表会按比例平滑移动，而不会跳动。仅在检测到精确式触控板时可用。");
    public static string TrackpadGesturesNotFound => T(
        "No precision trackpad detected on this PC, so it can't be turned on.",
        "No se ha detectado un trackpad de precisión en este equipo, así que no se puede activar.",
        "Auf diesem PC wurde kein Präzisions-Touchpad erkannt, daher kann es nicht aktiviert werden.",
        "Aucun pavé tactile de précision n'a été détecté sur ce PC, impossible de l'activer.",
        "Nenhum touchpad de precisão detectado neste PC, portanto não pode ser ativado.", "Su questo PC non è stato rilevato alcun trackpad di precisione, quindi non si può attivare.", "このパソコンでは高精度トラックパッドが検出されないため、オンにできません。", "此电脑上未检测到精确式触控板，因此无法开启。");
    public static string NewNoteTooltip => T("New note", "Nueva nota",
        "Neue Notiz", "Nouvelle note", "Nova nota", "Nuova nota", "新しいノート", "新笔记");

    // Segunda línea de los tooltips del pie: los cuatro botones tienen clic derecho y nada lo decía.
    public static string OpenAllRightClickHint => T("Right-click: layout and screen", "Clic derecho: disposición y pantalla",
        "Rechtsklick: Layout und Bildschirm",
        "Clic droit : disposition et écran",
        "Clique direito: layout e tela", "Clic destro: disposizione e schermo", "右クリック: 配置と画面", "右键：布局和屏幕");
    public static string ManageNotesRightClickHint => T("Right-click: view and tags", "Clic derecho: vista y etiquetas",
        "Rechtsklick: Ansicht und Tags",
        "Clic droit : affichage et étiquettes",
        "Clique direito: exibição e etiquetas", "Clic destro: vista ed etichette", "右クリック: 表示とタグ", "右键：视图和标签");
    public static string SyncRightClickHint => T("Right-click: sync settings", "Clic derecho: ajustes de sincronización",
        "Rechtsklick: Synchronisierungseinstellungen",
        "Clic droit : paramètres de synchronisation",
        "Clique direito: configurações de sincronização", "Clic destro: impostazioni di sincronizzazione", "右クリック: 同期の設定", "右键：同步设置");
    /// <summary>Carpeta de la línea de prompt de la piel bash (<c>usuario@aldune:~/notas$</c>).</summary>
    public static string PromptNotesFolder => T("notes", "notas", "notizen", "notes", "notas", "note", "ノート", "笔记");
    public static string NewNoteRightClickHint => T("Right-click: from the clipboard", "Clic derecho: desde el portapapeles",
        "Rechtsklick: aus der Zwischenablage",
        "Clic droit : depuis le presse-papiers",
        "Clique direito: da área de transferência", "Clic destro: dagli appunti", "右クリック: クリップボードから", "右键：来自剪贴板");
    public static string WithHint(string tooltip, string hint) => tooltip + Environment.NewLine + hint;
    public static string NewNoteFromClipboard => T("New note from clipboard", "Nueva nota desde el portapapeles",
        "Neue Notiz aus der Zwischenablage",
        "Nouvelle note depuis le presse-papiers",
        "Nova nota da área de transferência", "Nuova nota dagli appunti", "クリップボードから新しいノート", "从剪贴板新建笔记");
    public static string KeepDockOpenOn => T("✓  Keep dock open", "✓  Mantener el dock abierto",
        "✓  Dock geöffnet lassen", "✓  Garder le dock ouvert", "✓  Manter o dock aberto", "✓  Mantieni il dock aperto", "✓  ドックを開いたままにする", "✓  保持程序坞展开");
    public static string KeepDockOpenOff => T("Keep dock open", "Mantener el dock abierto",
        "Dock geöffnet lassen", "Garder le dock ouvert", "Manter o dock aberto", "Mantieni il dock aperto", "ドックを開いたままにする", "保持程序坞展开");
    public static string NewNoteTaggedTooltip(string tag) => T(
        $"New note with the tag \"{tag}\"", $"Nueva nota con la etiqueta \"{tag}\"",
        $"Neue Notiz mit dem Tag \"{tag}\"",
        $"Nouvelle note avec l'étiquette \"{tag}\"",
        $"Nova nota com a etiqueta \"{tag}\"", $"Nuova nota con l'etichetta \"{tag}\"", $"タグ「{tag}」の新しいノート", $"新建带有标签“{tag}”的笔记");
    public static string OpenAllNotesTaggedTooltip(string tag) => T(
        $"Open the notes with the tag \"{tag}\" (closes them if they're already open)",
        $"Abrir las notas con la etiqueta \"{tag}\" (las cierra si ya están abiertas)",
        $"Notizen mit dem Tag \"{tag}\" öffnen (schließt sie, falls sie bereits geöffnet sind)",
        $"Ouvrir les notes avec l'étiquette \"{tag}\" (les ferme si elles sont déjà ouvertes)",
        $"Abrir as notas com a etiqueta \"{tag}\" (fecha se já estiverem abertas)", $"Apri le note con l'etichetta \"{tag}\" (le chiude se sono già aperte)", $"タグ「{tag}」のノートを開く（すでに開いている場合は閉じる）", $"打开带有标签“{tag}”的笔记（如果已打开，则关闭）");
    public static string ExitAppTooltip => T("Exit Aldune", "Salir de Aldune",
        "Aldune beenden", "Quitter Aldune", "Sair do Aldune", "Esci da Aldune", "Aldune を終了", "退出 Aldune");
    public static string ScrollNotesUpTooltip => T("Show earlier notes", "Ver notas anteriores",
        "Vorherige Notizen anzeigen", "Afficher les notes précédentes", "Ver notas anteriores", "Mostra le note precedenti", "前のノートを表示", "显示更早的笔记");
    public static string ScrollNotesDownTooltip => T("Show later notes", "Ver notas siguientes",
        "Nächste Notizen anzeigen", "Afficher les notes suivantes", "Ver notas seguintes", "Mostra le note successive", "次のノートを表示", "显示更新的笔记");
    public static string OpenNote => T("Open", "Abrir",
        "Öffnen", "Ouvrir", "Abrir", "Apri", "開く", "打开");
    // «Fijar arriba» (la nota va la primera de las listas y viaja por la sync): distinto del «siempre encima» de arriba.
    public static string PinToTop => T("Pin to the top of the list", "Fijar arriba en la lista",
        "Oben in der Liste anheften", "Épingler en haut de la liste", "Fixar no topo da lista", "Fissa in cima all'elenco", "リストの先頭に固定", "固定在列表顶部");
    public static string UnpinFromTop => T("Unpin from the top of the list", "Quitar de arriba en la lista",
        "Von der Listenspitze lösen", "Détacher du haut de la liste", "Soltar do topo da lista", "Togli dalla cima dell'elenco", "リストの先頭への固定を解除", "取消固定在列表顶部");
    // Historial de versiones de una nota (local, 20 como mucho).
    public static string HistoryMenu => T("Version history…", "Historial de versiones…",
        "Versionsverlauf…", "Historique des versions…", "Histórico de versões…", "Cronologia delle versioni…", "バージョン履歴…", "版本历史…");
    public static string HistoryTitle => T("Version history", "Historial de versiones",
        "Versionsverlauf", "Historique des versions", "Histórico de versões", "Cronologia delle versioni", "バージョン履歴", "版本历史");
    public static string HistoryHint => T("Up to 20 earlier versions of this note, kept on this device only. Restoring first saves the current text, so you can undo it.",
        "Hasta 20 versiones anteriores de esta nota, guardadas solo en este dispositivo. Restaurar guarda antes el texto actual, así se puede deshacer.",
        "Bis zu 20 frühere Versionen dieser Notiz, nur auf diesem Gerät gespeichert. Beim Wiederherstellen wird zuerst der aktuelle Text gesichert, sodass man es rückgängig machen kann.",
        "Jusqu'à 20 versions précédentes de cette note, conservées sur cet appareil uniquement. Restaurer enregistre d'abord le texte actuel, ce qui permet d'annuler.",
        "Até 20 versões anteriores desta nota, guardadas apenas neste dispositivo. Restaurar guarda antes o texto atual, então dá para desfazer.", "Fino a 20 versioni precedenti di questa nota, conservate solo su questo dispositivo. Il ripristino salva prima il testo attuale, così puoi annullarlo.", "このノートの過去のバージョンを最大 20 件、このデバイスにのみ保存します。復元すると、先に現在のテキストが保存されるので、元に戻せます。", "保留这条笔记最多 20 个早期版本，仅保存在此设备上。恢复时会先保存当前文本，因此可以撤销。");
    public static string HistoryEmpty => T("No earlier versions yet. They appear as you edit the note.", "Aún no hay versiones anteriores. Aparecen al editar la nota.",
        "Noch keine früheren Versionen. Sie erscheinen beim Bearbeiten der Notiz.", "Pas encore de versions précédentes. Elles apparaissent quand vous modifiez la note.",
        "Ainda não há versões anteriores. Elas aparecem ao editar a nota.", "Ancora nessuna versione precedente. Compaiono mentre modifichi la nota.", "過去のバージョンはまだありません。ノートを編集すると表示されます。", "还没有早期版本。编辑笔记后会逐渐出现。");
    public static string HistoryRestore => T("Restore", "Restaurar", "Wiederherstellen", "Restaurer", "Restaurar", "Ripristina", "復元", "恢复");
    public static string HistoryClose => T("Close", "Cerrar", "Schließen", "Fermer", "Fechar", "Chiudi", "閉じる", "关闭");
    // Ajuste de «tareas hechas» por nota (un elemento de menú que cicla; las dos últimas nunca borran). Ver CompletedTasksPolicy.
    public static string DoneTasksFollowSettings => T("Done tasks: as in my settings", "Tareas hechas: según mis ajustes",
        "Erledigte Aufgaben: wie in meinen Einstellungen", "Tâches faites : selon mes réglages", "Tarefas feitas: conforme minhas configurações", "Attività completate: come nelle mie impostazioni", "完了したタスク: 設定に従う", "已完成任务：按我的设置");
    public static string DoneTasksKeepInPlace => T("Done tasks: stay in place", "Tareas hechas: se quedan en su sitio",
        "Erledigte Aufgaben: bleiben an ihrem Platz", "Tâches faites : restent en place", "Tarefas feitas: ficam no lugar", "Attività completate: restano dove sono", "完了したタスク: そのまま残す", "已完成任务：保持原位");
    public static string DoneTasksMoveToEnd => T("Done tasks: move to the bottom", "Tareas hechas: bajan al final",
        "Erledigte Aufgaben: wandern nach unten", "Tâches faites : descendent en bas", "Tarefas feitas: descem para o fim", "Attività completate: vanno in fondo", "完了したタスク: 末尾に移動", "已完成任务：移到底部");
    public static string DoneTasksPolicyTooltip => T("Click to change. The last two never delete anything, even if another device deletes done tasks.",
        "Pulsa para cambiar. Las dos últimas nunca borran nada, aunque otro dispositivo borre las tareas hechas.",
        "Zum Ändern klicken. Die letzten beiden löschen nie etwas, auch wenn ein anderes Gerät erledigte Aufgaben löscht.",
        "Cliquez pour changer. Les deux dernières ne suppriment jamais rien, même si un autre appareil supprime les tâches faites.",
        "Clique para mudar. As duas últimas nunca apagam nada, mesmo que outro dispositivo apague as tarefas feitas.", "Fai clic per cambiare. Le ultime due non cancellano mai nulla, anche se un altro dispositivo elimina le attività completate.", "クリックで切り替えます。後ろの 2 つは、別のデバイスが完了したタスクを削除しても、何も削除しません。", "点击切换。后两项永远不会删除任何内容，即使其他设备删除了已完成的任务。");
    public static string DoneTasksPolicyLabel(Aldune.Core.CompletedTasksPolicy policy) => policy switch
    {
        Aldune.Core.CompletedTasksPolicy.KeepInPlace => DoneTasksKeepInPlace,
        Aldune.Core.CompletedTasksPolicy.MoveToEnd => DoneTasksMoveToEnd,
        _ => DoneTasksFollowSettings
    };
    public static string PinnedToTopMarker => T("Pinned to the top of the list", "Fijada arriba en la lista",
        "Oben in der Liste angeheftet", "Épinglée en haut de la liste", "Fixada no topo da lista", "Fissata in cima all'elenco", "リストの先頭に固定済み", "已固定在列表顶部");
    public static string ProtectNote => T("Protect with password…", "Proteger con contraseña…",
        "Mit Passwort schützen…", "Protéger par mot de passe…", "Proteger com senha…", "Proteggi con password…", "パスワードで保護…", "用密码保护…");
    public static string VisibleNameMenu => T("Visible name…", "Nombre visible…",
        "Sichtbarer Name…", "Nom visible…", "Nome visível…", "Nome visibile…", "表示名…", "可见名称…");
    public static string VisibleNameTitle => T("Visible name", "Nombre visible",
        "Sichtbarer Name", "Nom visible", "Nome visível", "Nome visibile", "表示名", "可见名称");
    public static string VisibleNameHint => T(
        "Shown in the dock and in Manage notes instead of \"Protected note\", even while the note is locked. Anyone who sees your screen will see it. Leave it empty to hide it.",
        "Se muestra en el dock y en Gestionar notas en lugar de «Nota protegida», aunque la nota esté bloqueada. Cualquiera que vea tu pantalla lo verá. Déjalo vacío para ocultarlo.",
        "Wird im Dock und unter „Notizen verwalten“ statt „Geschützte Notiz“ angezeigt, auch wenn die Notiz gesperrt ist. Wer auf deinen Bildschirm schaut, sieht ihn. Leer lassen, um ihn auszublenden.",
        "S'affiche dans le dock et dans Gérer les notes à la place de « Note protégée », même verrouillée. Quiconque voit votre écran le verra. Laissez vide pour le masquer.",
        "Aparece no dock e em Gerenciar notas no lugar de \"Nota protegida\", mesmo com a nota bloqueada. Qualquer pessoa que veja sua tela o verá. Deixe vazio para ocultá-lo.", "Mostrato nel dock e in Gestisci le note al posto di \"Nota protetta\", anche mentre la nota è bloccata. Chiunque veda il tuo schermo lo vedrà. Lascialo vuoto per nasconderlo.", "ノートがロックされている間も、「保護されたノート」の代わりにドックと「ノートを管理」に表示されます。画面を見た人には誰でも見えます。隠すには空のままにしてください。", "即使笔记处于锁定状态，也会在程序坞和“管理笔记”中显示，以代替“受保护的笔记”。任何看到您屏幕的人都能看到。留空则隐藏。");
    public static string VisibleNameLabel => T("Visible name (optional)", "Nombre visible (opcional)",
        "Sichtbarer Name (optional)", "Nom visible (facultatif)", "Nome visível (opcional)", "Nome visibile (facoltativo)", "表示名（任意）", "可见名称（可选）");
    public static string VisibleNameAction => T("Save", "Guardar", "Speichern", "Enregistrer", "Salvar", "Salva", "保存", "保存");
    public static string RemoveProtection => T("Remove password protection", "Quitar protección con contraseña",
        "Passwortschutz aufheben", "Supprimer la protection par mot de passe", "Remover proteção por senha", "Rimuovi la protezione con password", "パスワード保護を解除", "取消密码保护");
    public static string UnlockNote => T("Unlock note", "Desbloquear nota",
        "Notiz entsperren", "Déverrouiller la note", "Desbloquear nota", "Sblocca nota", "ノートのロックを解除", "解锁笔记");
    public static string ProtectedNote => T("Protected note", "Nota protegida",
        "Geschützte Notiz", "Note protégée", "Nota protegida", "Nota protetta", "保護されたノート", "受保护的笔记");
    public static string ProtectedNoteHint => T("Enter the password to open this note. It is not stored by Aldune.", "Escribe la contraseña para abrir esta nota. Aldune no la guarda.",
        "Gib das Passwort ein, um diese Notiz zu öffnen. Es wird von Aldune nicht gespeichert.",
        "Entrez le mot de passe pour ouvrir cette note. Il n'est pas enregistré par Aldune.",
        "Digite a senha para abrir esta nota. Ela não é salva pelo Aldune.", "Inserisci la password per aprire questa nota. Aldune non la memorizza.", "このノートを開くにはパスワードを入力してください。パスワードは Aldune には保存されません。", "输入密码以打开这条笔记。Aldune 不会保存该密码。");
    public static string ProtectNoteHint => T("Choose a password for this note. If you lose it, the note cannot be recovered.", "Elige una contraseña para esta nota. Si la pierdes, no se podrá recuperar.",
        "Wähle ein Passwort für diese Notiz. Wenn du es verlierst, kann die Notiz nicht wiederhergestellt werden.",
        "Choisissez un mot de passe pour cette note. Si vous le perdez, la note ne pourra pas être récupérée.",
        "Escolha uma senha para esta nota. Se você a perder, a nota não poderá ser recuperada.", "Scegli una password per questa nota. Se la perdi, la nota non si può recuperare.", "このノートのパスワードを決めてください。忘れた場合、ノートは復元できません。", "为这条笔记设置密码。如果丢失密码，笔记将无法恢复。");
    public static string PasswordLabel => T("Password", "Contraseña",
        "Passwort", "Mot de passe", "Senha", "Password", "パスワード", "密码");
    public static string ConfirmPasswordLabel => T("Repeat password", "Repite la contraseña",
        "Passwort wiederholen", "Répétez le mot de passe", "Repita a senha", "Ripeti la password", "パスワードを再入力", "重复密码");
    public static string PasswordTooShort => T("Use at least 8 characters.", "Usa al menos 8 caracteres.",
        "Verwende mindestens 8 Zeichen.", "Utilisez au moins 8 caractères.", "Use pelo menos 8 caracteres.", "Usa almeno 8 caratteri.", "8 文字以上にしてください。", "至少使用 8 个字符。");
    public static string PasswordsDoNotMatch => T("The passwords do not match.", "Las contraseñas no coinciden.",
        "Die Passwörter stimmen nicht überein.",
        "Les mots de passe ne correspondent pas.",
        "As senhas não coincidem.", "Le password non coincidono.", "パスワードが一致しません。", "两次输入的密码不一致。");
    public static string WrongPassword => T("That password is not correct.", "Esa contraseña no es correcta.",
        "Dieses Passwort ist nicht korrekt.",
        "Ce mot de passe est incorrect.",
        "Essa senha não está correta.", "La password non è corretta.", "パスワードが正しくありません。", "密码不正确。");
    public static string Accept => T("Accept", "Aceptar",
        "Bestätigen", "Accepter", "Aceitar", "Accetta", "同意する", "接受");
    public static string ProtectNoteTitle => T("Protect with a password", "Proteger con contraseña",
        "Mit einem Passwort schützen", "Protéger par un mot de passe", "Proteger com uma senha", "Proteggi con una password", "パスワードで保護", "用密码保护");
    public static string ProtectAction => T("Protect", "Proteger",
        "Schützen", "Protéger", "Proteger", "Proteggi", "保護", "保护");
    public static string UnlockAction => T("Unlock", "Desbloquear",
        "Entsperren", "Déverrouiller", "Desbloquear", "Sblocca", "ロック解除", "解锁");
    public static string RemoveProtectionTitle => T("Remove protection", "Quitar la protección",
        "Schutz aufheben", "Supprimer la protection", "Remover a proteção", "Rimuovi la protezione", "保護を解除", "取消保护");
    public static string RemoveProtectionHint => T("Enter this note's password. It will stay encrypted with Aldune's own key, like any other note.", "Escribe la contraseña de esta nota. Seguirá cifrada con la clave de Aldune, como el resto de notas.",
        "Gib das Passwort dieser Notiz ein. Sie bleibt mit dem eigenen Schlüssel von Aldune verschlüsselt, wie jede andere Notiz auch.",
        "Entrez le mot de passe de cette note. Elle restera chiffrée avec la clé propre d'Aldune, comme toute autre note.",
        "Digite a senha desta nota. Ela continuará criptografada com a própria chave do Aldune, como qualquer outra nota.", "Inserisci la password di questa nota. Resterà cifrata con la chiave di Aldune, come qualsiasi altra nota.", "このノートのパスワードを入力してください。他のノートと同様に、Aldune 独自のキーで暗号化されたままです。", "输入这条笔记的密码。它仍会像其他笔记一样，使用 Aldune 自己的密钥加密。");
    public static string RemoveProtectionAction => T("Remove protection", "Quitar protección",
        "Schutz aufheben", "Supprimer la protection", "Remover proteção", "Rimuovi la protezione", "保護を解除", "取消保护");
    public static string PasswordEmpty => T("Type the password.", "Escribe la contraseña.",
        "Gib das Passwort ein.", "Tapez le mot de passe.", "Digite a senha.", "Digita la password.", "パスワードを入力してください。", "请输入密码。");
    public static string ShowPassword => T("Show password", "Mostrar contraseña",
        "Passwort anzeigen", "Afficher le mot de passe", "Mostrar senha", "Mostra la password", "パスワードを表示", "显示密码");
    public static string HidePassword => T("Hide password", "Ocultar contraseña",
        "Passwort ausblenden", "Masquer le mot de passe", "Ocultar senha", "Nascondi la password", "パスワードを隠す", "隐藏密码");
    public static string CapsLockOn => T("Caps Lock is on.", "Bloq Mayús está activado.",
        "Feststelltaste ist aktiviert.", "Verr Maj est activé.", "O Caps Lock está ativado.", "Il Blocco maiuscole è attivo.", "Caps Lock がオンです。", "大写锁定已开启。");
    public static string DockViewTooltip => T("Dock view", "Vista del dock",
        "Dock-Ansicht", "Vue du dock", "Exibição do dock", "Vista del dock", "ドックの表示", "程序坞视图");
    public static string DockViewTitle => T("Show in dock", "Mostrar en el dock",
        "Im Dock anzeigen", "Afficher dans le dock", "Mostrar no dock", "Mostra nel dock", "ドックに表示", "在程序坞中显示");
    public static string DockViewActive => T("Active notes", "Notas activas",
        "Aktive Notizen", "Notes actives", "Notas ativas", "Note attive", "アクティブなノート", "活动笔记");
    public static string WheelChangesViewCheckbox => T("Change view with the mouse wheel", "Cambiar de vista con la rueda",
        "Ansicht mit dem Mausrad wechseln", "Changer de vue avec la molette", "Trocar de exibição com a roda do mouse", "Cambia vista con la rotellina del mouse", "マウスホイールで表示を切り替える", "用鼠标滚轮切换视图");
    public static string WheelChangesViewHint => T(
        "Scroll over the gear to switch between all notes, each tag, archived and trash. Over the tabs it only switches when every note fits; with Ctrl it always does.",
        "Gira la rueda sobre el engranaje para pasar entre notas, cada etiqueta, archivadas y papelera. Sobre las pestañas solo cambia si caben todas las notas; con Ctrl siempre.",
        "Mausrad über dem Zahnrad wechselt zwischen allen Notizen, jedem Tag, Archiv und Papierkorb. Über den Reitern nur, wenn alle Notizen passen; mit Strg immer.",
        "La molette sur l'engrenage passe entre toutes les notes, chaque étiquette, archivées et corbeille. Sur les onglets, seulement si toutes les notes tiennent ; avec Ctrl, toujours.",
        "A roda sobre a engrenagem alterna entre todas as notas, cada etiqueta, arquivadas e lixeira. Sobre as abas só troca se todas as notas couberem; com Ctrl sempre.", "Scorri sull'ingranaggio per passare tra tutte le note, ogni etichetta, archiviate e cestino. Sulle schede cambia solo se entrano tutte le note; con Ctrl lo fa sempre.", "歯車の上でスクロールすると、すべてのノート、各タグ、アーカイブ、ごみ箱を切り替えられます。タブの上では、すべてのノートが収まっているときだけ切り替わります。Ctrl を押すと常に切り替わります。", "在齿轮上滚动，可在所有笔记、各个标签、已归档和回收站之间切换。在标签页上，只有当所有笔记都能放下时才会切换；按住 Ctrl 则始终切换。");
    public static string DockViewAll => T("All notes", "Todas las notas",
        "Alle Notizen", "Toutes les notes", "Todas as notas", "Tutte le note", "すべてのノート", "所有笔记");
    public static string DockViewArchived => T("Archived notes", "Notas archivadas",
        "Archivierte Notizen", "Notes archivées", "Notas arquivadas", "Note archiviate", "アーカイブしたノート", "已归档的笔记");
    public static string DockViewTrash => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira", "Cestino", "ごみ箱", "回收站");
    public static string DockViewTags => T("By tag", "Por etiqueta",
        "Nach Tag", "Par étiquette", "Por etiqueta", "Per etichetta", "タグ別", "按标签");
    public static string DockViewAllTags => T("Choose a tag", "Elegir una etiqueta",
        "Tag auswählen", "Choisir une étiquette", "Escolher uma etiqueta", "Scegli un'etichetta", "タグを選択", "选择标签");
    public static string DockViewNoTags => T("No tags yet", "Aún no hay etiquetas",
        "Noch keine Tags", "Aucune étiquette pour l'instant", "Nenhuma etiqueta ainda", "Ancora nessuna etichetta", "タグはまだありません", "还没有标签");
    public static string NoteTags => T("Tags…", "Etiquetas…",
        "Tags…", "Étiquettes…", "Etiquetas…", "Etichette…", "タグ…", "标签…");
    public static string AddTags => T("+ Tags", "+ Etiquetas",
        "+ Tags", "+ Étiquettes", "+ Etiquetas", "+ Etichette", "+ タグ", "+ 标签");
    public static string EditTagsTooltip => T("Edit tags", "Editar etiquetas",
        "Tags bearbeiten", "Modifier les étiquettes", "Editar etiquetas", "Modifica etichette", "タグを編集", "编辑标签");
    public static string SortTooltip => T("Sort", "Ordenar",
        "Sortieren", "Trier", "Ordenar", "Ordina", "並べ替え", "排序");
    public static string SortDock => T("Dock order", "Orden del mazo",
        "Dock-Reihenfolge", "Ordre du dock", "Ordem do dock", "Ordine del dock", "ドックの順序", "程序坞顺序");
    public static string SortNewest => T("Recent first", "Más recientes",
        "Neueste zuerst", "Les plus récents d'abord", "Mais recentes primeiro", "Prima le più recenti", "新しい順", "最新的在前");
    public static string SortOldest => T("Oldest first", "Más antiguas",
        "Älteste zuerst", "Les plus anciens d'abord", "Mais antigas primeiro", "Prima le più vecchie", "古い順", "最早的在前");
    public static string SortTitle => T("Title (A–Z)", "Título (A-Z)",
        "Titel (A–Z)", "Titre (A–Z)", "Título (A–Z)", "Titolo (A–Z)", "タイトル順 (A–Z)", "标题 (A–Z)");
    public static string SyncInsecureUrlWarning => T(
        "This address uses http:// outside your local network: your notes stay encrypted, but the access token or password travels unprotected. Use https://.",
        "Esta dirección usa http:// fuera de tu red local: las notas siguen cifradas, pero el token o la contraseña viajan sin protección. Usa https://.",
        "Diese Adresse verwendet http:// außerhalb deines lokalen Netzwerks: Die Notizen bleiben verschlüsselt, aber das Zugriffstoken oder Passwort wird ungeschützt übertragen. Verwende https://.",
        "Cette adresse utilise http:// en dehors de ton réseau local : vos notes restent chiffrées, mais le jeton d'accès ou le mot de passe circule sans protection. Utilisez https://.",
        "Este endereço usa http:// fora da sua rede local: suas notas permanecem criptografadas, mas o token de acesso ou a senha trafegam sem proteção. Use https://.", "Questo indirizzo usa http:// fuori dalla tua rete locale: le note restano cifrate, ma il token di accesso o la password viaggia senza protezione. Usa https://.", "このアドレスはローカルネットワーク外で http:// を使っています。ノートは暗号化されたままですが、アクセストークンやパスワードは保護されずに送信されます。https:// を使ってください。", "此地址在本地网络之外使用 http://：您的笔记仍会加密，但访问令牌或密码会不受保护地传输。请使用 https://。");
    public static string HowToUseButton => T("How it works", "Cómo se usa", "So funktioniert's", "Comment ça marche", "Como usar", "Come funziona", "使い方", "使用方法");
    public static string HowToUseBody => T(
        "• The dock: move the mouse to the edge of the screen and click + to create a note.\n" +
        "• Tasks: start a line with ☐ and press Ctrl+Enter to check it off.\n" +
        "• Files: drag a .md or .txt file onto the dock to edit it as a linked note, or import it as a normal note from the + menu.\n" +
        "• Several devices: in Settings you can sync your notes, always end-to-end encrypted.",
        "• El dock: lleva el ratón al borde de la pantalla y pulsa + para crear una nota.\n" +
        "• Tareas: empieza una línea con ☐ y pulsa Ctrl+Enter para marcarla.\n" +
        "• Archivos: arrastra un .md o .txt sobre el dock para editarlo como nota vinculada, o impórtalo como nota normal desde el menú del +.\n" +
        "• Varios dispositivos: en Ajustes puedes sincronizar tus notas, siempre cifradas de extremo a extremo.",
        "• Der Dock: Bewege die Maus an den Bildschirmrand und klicke auf +, um eine Notiz zu erstellen.\n" +
        "• Aufgaben: Beginne eine Zeile mit ☐ und drücke Strg+Eingabe, um sie abzuhaken.\n" +
        "• Dateien: Ziehe eine .md- oder .txt-Datei auf den Dock, um sie als verknüpfte Notiz zu bearbeiten, oder importiere sie über das +-Menü als normale Notiz.\n" +
        "• Mehrere Geräte: In den Einstellungen kannst du deine Notizen synchronisieren, stets Ende-zu-Ende-verschlüsselt.",
        "• Le dock : déplacez la souris au bord de l'écran et cliquez sur + pour créer une note.\n" +
        "• Tâches : commencez une ligne par ☐ et appuyez sur Ctrl+Entrée pour la cocher.\n" +
        "• Fichiers : glissez un fichier .md ou .txt sur le dock pour le modifier comme note liée, ou importez-le comme note normale depuis le menu +.\n" +
        "• Plusieurs appareils : dans les paramètres, vous pouvez synchroniser vos notes, toujours chiffrées de bout en bout.",
        "• O dock: mova o mouse até a borda da tela e clique em + para criar uma nota.\n" +
        "• Tarefas: comece uma linha com ☐ e pressione Ctrl+Enter para marcá-la.\n" +
        "• Arquivos: arraste um arquivo .md ou .txt para o dock para editá-lo como nota vinculada, ou importe-o como nota normal pelo menu +.\n" +
        "• Vários dispositivos: em Configurações você pode sincronizar suas notas, sempre com criptografia de ponta a ponta.", "• Il dock: porta il mouse sul bordo dello schermo e fai clic su + per creare una nota.\n• Attività: inizia una riga con ☐ e premi Ctrl+Invio per spuntarla.\n• File: trascina un file .md o .txt sul dock per modificarlo come nota collegata, oppure importalo come nota normale dal menu +.\n• Più dispositivi: nelle Impostazioni puoi sincronizzare le note, sempre con cifratura end-to-end.", "• ドック: マウスを画面の端に動かして + をクリックすると、ノートを作成できます。\n• タスク: 行頭に ☐ を入力し、Ctrl+Enter でチェックします。\n• ファイル: .md や .txt ファイルをドックにドラッグすると、リンクされたノートとして編集できます。+ メニューから通常のノートとしてインポートすることもできます。\n• 複数のデバイス: 設定でノートを同期できます。常にエンドツーエンドで暗号化されます。", "• 程序坞：把鼠标移到屏幕边缘，点击 + 即可创建笔记。\n• 任务：在一行开头输入 ☐，然后按 Ctrl+Enter 勾选。\n• 文件：把 .md 或 .txt 文件拖到程序坞，即可作为链接笔记进行编辑；也可以通过 + 菜单将其作为普通笔记导入。\n• 多台设备：在设置中可以同步您的笔记，始终采用端到端加密。");
    public static string WelcomeTitle => T("Aldune is ready", "Aldune está lista",
        "Aldune ist bereit", "Aldune est prêt", "Aldune está pronto", "Aldune è pronto", "Aldune の準備ができました", "Aldune 已准备就绪");
    public static string WelcomeMessage(Aldune.Core.EdgePosition edge, string? hotkey)
    {
        var (en, es, de, fr, pt, it, ja, zh) = edge switch
        {
            Aldune.Core.EdgePosition.Left => ("left", "izquierdo", "linken", "gauche", "esquerda", "sinistro", "左", "左"),
            Aldune.Core.EdgePosition.Top => ("top", "superior", "oberen", "supérieur", "superior", "superiore", "上", "上"),
            Aldune.Core.EdgePosition.Bottom => ("bottom", "inferior", "unteren", "inférieur", "inferior", "inferiore", "下", "下"),
            _ => ("right", "derecho", "rechten", "droit", "direita", "destro", "右", "右"),
        };
        return hotkey is null
            ? T($"Your notes live on the {en} edge of the screen: move the mouse there and click + to create one.",
                $"Tus notas viven en el borde {es} de la pantalla: lleva el ratón ahí y pulsa + para crear una.",
                $"Deine Notizen leben am {de} Bildschirmrand: Bewege die Maus dorthin und klicke auf +, um eine zu erstellen.",
                $"Vos notes se trouvent sur le bord {fr} de l'écran : déplacez la souris là et cliquez sur + pour en créer une.",
                $"Suas notas ficam na borda {pt} da tela: mova o mouse até lá e clique em + para criar uma.", $"Le tue note vivono sul bordo {it} dello schermo: porta il mouse lì e fai clic su + per crearne una.", $"ノートは画面の{ja}端にあります。マウスをそこに動かして + をクリックすると、ノートを作成できます。", $"您的笔记位于屏幕的{zh}边缘：把鼠标移到那里并点击 + 即可创建笔记。")
            : T($"Your notes live on the {en} edge of the screen: move the mouse there, or press {hotkey} to create one.",
                $"Tus notas viven en el borde {es} de la pantalla: lleva el ratón ahí, o pulsa {hotkey} para crear una.",
                $"Deine Notizen leben am {de} Bildschirmrand: Bewege die Maus dorthin oder drücke {hotkey}, um eine zu erstellen.",
                $"Vos notes se trouvent sur le bord {fr} de l'écran : déplacez la souris là ou appuyez sur {hotkey} pour en créer une.",
                $"Suas notas ficam na borda {pt} da tela: mova o mouse até lá ou pressione {hotkey} para criar uma.", $"Le tue note vivono sul bordo {it} dello schermo: porta il mouse lì, oppure premi {hotkey} per crearne una.", $"ノートは画面の{ja}端にあります。マウスをそこに動かすか、{hotkey} を押すと、ノートを作成できます。", $"您的笔记位于屏幕的{zh}边缘：把鼠标移到那里，或按 {hotkey} 即可创建笔记。");
    }
    public static string TagPanelTitle => T("Tags", "Etiquetas",
        "Tags", "Tags", "Tags", "Etichette", "タグ", "标签");
    public static string TagPanelHint => T("Changes are saved as you tick them.", "Se guardan al marcarlas.",
        "Die Änderungen werden beim Abhaken gespeichert.",
        "Les modifications sont enregistrées au fur et à mesure.",
        "As alterações são salvas ao marcá-las.", "Le modifiche vengono salvate mentre le spunti.", "チェックすると変更はすぐに保存されます。", "勾选后更改会立即保存。");
    public static string TagPanelEmpty => T("No tags yet. Create the first one below.", "Aún no hay etiquetas. Crea la primera aquí debajo.",
        "Noch keine Tags. Erstelle unten den ersten.",
        "Aucun tag pour l'instant. Créez le premier ci-dessous.",
        "Nenhuma tag ainda. Crie a primeira abaixo.", "Ancora nessuna etichetta. Crea la prima qui sotto.", "タグはまだありません。下で最初のタグを作成してください。", "还没有标签。请在下方创建第一个。");
    public static string NewTagPlaceholder => T("New tag", "Nueva etiqueta",
        "Neuer Tag", "Nouveau tag", "Nova tag", "Nuova etichetta", "新しいタグ", "新标签");
    public static string ManageTags => T("Manage tags", "Gestionar etiquetas",
        "Tags verwalten", "Gérer les tags", "Gerenciar tags", "Gestisci le etichette", "タグを管理", "管理标签");
    public static string NewTag => T("New tag", "Nueva etiqueta",
        "Neuer Tag", "Nouveau tag", "Nova tag", "Nuova etichetta", "新しいタグ", "新标签");
    public static string CreateTag => T("Create", "Crear",
        "Erstellen", "Créer", "Criar", "Crea", "作成", "创建");
    public static string DeleteTag => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir", "Elimina", "削除", "删除");
    public static string DeleteTagTooltip => T("Delete tag", "Eliminar etiqueta",
        "Tag löschen", "Supprimer le tag", "Excluir tag", "Elimina etichetta", "タグを削除", "删除标签");
    public static string AllTags => T("All tags", "Todas las etiquetas",
        "Alle Tags", "Tous les tags", "Todas as tags", "Tutte le etichette", "すべてのタグ", "所有标签");
    public static string TagFilterTooltip => T("Filter by tag", "Filtrar por etiqueta",
        "Nach Tag filtern", "Filtrer par tag", "Filtrar por tag", "Filtra per etichetta", "タグで絞り込む", "按标签筛选");
    public static string NoTagResults(string tag) =>
        T($"No notes have the tag “{tag}”.", $"Ninguna nota tiene la etiqueta «{tag}».",
            $"Keine Notiz hat den Tag „{tag}“.",
            $"Aucune note n'a le tag « {tag} ».",
            $"Nenhuma nota tem a tag “{tag}”.", $"Nessuna nota ha l'etichetta “{tag}”.", $"タグ「{tag}」のノートはありません。", $"没有笔记带有标签“{tag}”。");

    // --- Gestor de notas ---------------------------------------------------------------------------

    public static string ManageNotesTitle => T("Manage notes", "Gestionar notas",
        "Notizen verwalten", "Gérer les notes", "Gerenciar notas", "Gestisci le note", "ノートを管理", "管理笔记");
    public static string SettingsTooltip => T("Settings", "Ajustes",
        "Einstellungen", "Paramètres", "Configurações", "Impostazioni", "設定", "设置");
    public static string FilterActive => T("Active", "Activas",
        "Aktiv", "Actives", "Ativas", "Attive", "アクティブ", "活动");
    public static string FilterArchived => T("Archived", "Archivadas",
        "Archiviert", "Archivées", "Arquivadas", "Archiviate", "アーカイブ済み", "已归档");
    public static string FilterTrashed => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira", "Cestino", "ごみ箱", "回收站");
    public static string SearchPlaceholder => T("Search your notes…", "Buscar en tus notas…",
        "Notizen durchsuchen…", "Rechercher dans vos notes…", "Pesquisar nas suas notas…", "Cerca nelle tue note…", "ノートを検索…", "搜索您的笔记…");
    // --- Buscar dentro de una nota (Ctrl+F) ---------------------------------------------------------

    public static string FindInNotePlaceholder => T("Find in note…", "Buscar en la nota…",
        "In der Notiz suchen…", "Rechercher dans la note…", "Buscar na nota…", "Trova nella nota…", "ノート内を検索…", "在笔记中查找…");
    public static string FindPreviousTooltip => T("Previous (Shift+Enter)", "Anterior (Mayús+Intro)",
        "Vorheriger (Umschalt+Eingabe)", "Précédent (Maj+Entrée)", "Anterior (Shift+Enter)", "Precedente (Maiusc+Invio)", "前へ (Shift+Enter)", "上一个 (Shift+Enter)");
    public static string FindNextTooltip => T("Next (Enter)", "Siguiente (Intro)",
        "Nächster (Eingabe)", "Suivant (Entrée)", "Próximo (Enter)", "Successivo (Invio)", "次へ (Enter)", "下一个 (Enter)");
    public static string FindCloseTooltip => T("Close (Esc)", "Cerrar (Esc)",
        "Schließen (Esc)", "Fermer (Échap)", "Fechar (Esc)", "Chiudi (Esc)", "閉じる (Esc)", "关闭 (Esc)");
    public static string FindNoResults => T("No results", "Sin resultados",
        "Keine Treffer", "Aucun résultat", "Sem resultados", "Nessun risultato", "結果なし", "无结果");

    public static string ClearSearchTooltip => T("Clear search", "Borrar búsqueda",
        "Suche löschen", "Effacer la recherche", "Limpar pesquisa", "Cancella ricerca", "検索をクリア", "清除搜索");
    public static string SelectAll => T("Select all", "Seleccionar todo",
        "Alle auswählen", "Tout sélectionner", "Selecionar tudo", "Seleziona tutto", "すべて選択", "全选");
    public static string Delete => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir", "Elimina", "削除", "删除");
    public static string DeletePermanentlyTitle => T("Delete permanently", "Eliminar definitivamente",
        "Endgültig löschen", "Supprimer définitivement", "Excluir permanentemente", "Elimina definitivamente", "完全に削除", "永久删除");
    public static string Export => T("Export", "Exportar",
        "Exportieren", "Exporter", "Exportar", "Esporta", "エクスポート", "导出");
    public static string ExportFolderDialogTitle => T("Choose a folder to export to", "Elige una carpeta donde exportar",
        "Ordner zum Exportieren wählen",
        "Choisir un dossier d'exportation",
        "Escolha uma pasta para exportar", "Scegli una cartella in cui esportare", "エクスポート先のフォルダーを選択", "选择导出位置的文件夹");
    public static string ExportFormatTitle => T("Export notes", "Exportar notas",
        "Notizen exportieren", "Exporter les notes", "Exportar notas", "Esporta note", "ノートをエクスポート", "导出笔记");
    public static string ExportAsZipPrompt => T(
        "Export as a single .zip file?\n\nChoose “No” to get loose .md files in a folder instead.",
        "¿Exportar como un único archivo .zip?\n\nElige «No» para tener los archivos .md sueltos en una carpeta.",
        "Als einzelne .zip-Datei exportieren?\n\nWähle „Nein“, um stattdessen lose .md-Dateien in einem Ordner zu erhalten.",
        "Exporter en un seul fichier .zip ?\n\nChoisissez « Non » pour obtenir des fichiers .md séparés dans un dossier.",
        "Exportar como um único arquivo .zip?\n\nEscolha “Não” para obter arquivos .md soltos em uma pasta.", "Esportare come un unico file .zip?\n\nScegli “No” per ottenere invece file .md separati in una cartella.", "1 つの .zip ファイルとしてエクスポートしますか？\n\n「いいえ」を選ぶと、フォルダーに個別の .md ファイルを書き出します。", "导出为单个 .zip 文件吗？\n\n选择“否”则改为在文件夹中生成单独的 .md 文件。");
    public static string ZipFileFilter => T("Zip file (*.zip)|*.zip", "Archivo zip (*.zip)|*.zip",
        "Zip-Datei (*.zip)|*.zip", "Fichier zip (*.zip)|*.zip", "Arquivo zip (*.zip)|*.zip", "File zip (*.zip)|*.zip", "Zip ファイル (*.zip)|*.zip", "Zip 文件 (*.zip)|*.zip");

    public static string OneNote => T("1 note", "1 nota",
        "1 Notiz", "1 note", "1 nota", "1 nota", "1 件のノート", "1 条笔记");
    public static string NotesCount(int count) => T($"{count} notes", $"{count} notas",
        $"{count} Notizen", $"{count} notes", $"{count} notas", $"{count} note", $"{count} 件のノート", $"{count} 条笔记");
    public static string OneSelected => T("1 selected", "1 seleccionada",
        "1 ausgewählt", "1 sélectionnée", "1 selecionada", "1 selezionata", "1 件を選択中", "已选择 1 条");
    public static string SelectedCount(int count) => T($"{count} selected", $"{count} seleccionadas",
        $"{count} ausgewählt", $"{count} sélectionnées", $"{count} selecionadas", $"{count} selezionate", $"{count} 件を選択中", $"已选择 {count} 条");

    public static string NoSearchResults(string query) =>
        T($"No note contains “{query}”.", $"Ninguna nota contiene «{query}».",
            $"Keine Notiz enthält „{query}“.",
            $"Aucune note ne contient « {query} ».",
            $"Nenhuma nota contém “{query}”.", $"Nessuna nota contiene “{query}”.", $"「{query}」を含むノートはありません。", $"没有笔记包含“{query}”。");
    public static string EmptyActive => T("No active notes. Create one with the + button on the screen edge.", "No hay notas activas. Crea una con el botón + del borde de la pantalla.",
        "Keine aktiven Notizen. Erstelle eine mit dem +-Knopf am Bildschirmrand.",
        "Aucune note active. Créez-en une avec le bouton + sur le bord de l'écran.",
        "Nenhuma nota ativa. Crie uma com o botão + na borda da tela.", "Nessuna nota attiva. Creane una con il pulsante + sul bordo dello schermo.", "アクティブなノートはありません。画面の端にある + ボタンで作成してください。", "没有活动笔记。请用屏幕边缘的 + 按钮创建一条。");
    public static string EmptyArchived => T("You haven't archived any notes yet.", "No has archivado ninguna nota todavía.",
        "Du hast noch keine Notizen archiviert.",
        "Vous n'avez pas encore archivé de notes.",
        "Você ainda não arquivou nenhuma nota.", "Non hai ancora archiviato nessuna nota.", "アーカイブしたノートはまだありません。", "您还没有归档任何笔记。");
    public static string EmptyTrashed => T("Trash is empty. Anything you send here is deleted after 30 days.", "La papelera está vacía. Lo que envíes aquí se borra solo a los 30 días.",
        "Der Papierkorb ist leer. Alles, was du hierher sendest, wird nach 30 Tagen gelöscht.",
        "La corbeille est vide. Tout ce que vous y envoyez est supprimé après 30 jours.",
        "A lixeira está vazia. Tudo que você enviar aqui será excluído após 30 dias.", "Il cestino è vuoto. Tutto ciò che invii qui viene eliminato dopo 30 giorni.", "ごみ箱は空です。ここに入れたものは 30 日後に削除されます。", "回收站是空的。放到这里的内容会在 30 天后删除。");
    public static string EmptyNone => T("There are no notes yet. Create one with the + button on the screen edge.", "Todavía no hay notas. Crea una con el botón + del borde de la pantalla.",
        "Es gibt noch keine Notizen. Erstelle eine mit dem +-Knopf am Bildschirmrand.",
        "Aucune note pour l'instant. Créez-en une avec le bouton + sur le bord de l'écran.",
        "Ainda não há notas. Crie uma com o botão + na borda da tela.", "Non ci sono ancora note. Creane una con il pulsante + sul bordo dello schermo.", "ノートはまだありません。画面の端にある + ボタンで作成してください。", "还没有笔记。请用屏幕边缘的 + 按钮创建一条。");

    public static string ConfirmDeleteOne => T("1 note will be permanently deleted. This action cannot be undone.", "Se eliminará 1 nota definitivamente. Esta acción no se puede deshacer.",
        "1 Notiz wird endgültig gelöscht. Diese Aktion kann nicht rückgängig gemacht werden.",
        "1 note sera supprimée définitivement. Cette action est irréversible.",
        "1 nota será excluída permanentemente. Esta ação não pode ser desfeita.", "1 nota verrà eliminata definitivamente. Questa azione non può essere annullata.", "1 件のノートが完全に削除されます。この操作は元に戻せません。", "1 条笔记将被永久删除。此操作无法撤销。");
    public static string ConfirmDeleteMany(int count) =>
        T($"{count} notes will be permanently deleted. This action cannot be undone.",
          $"Se eliminarán {count} notas definitivamente. Esta acción no se puede deshacer.",
            $"{count} Notizen werden endgültig gelöscht. Diese Aktion kann nicht rückgängig gemacht werden.",
            $"{count} notes seront supprimées définitivement. Cette action est irréversible.",
            $"{count} notas serão excluídas permanentemente. Esta ação não pode ser desfeita.", $"{count} note verranno eliminate definitivamente. Questa azione non può essere annullata.", $"{count} 件のノートが完全に削除されます。この操作は元に戻せません。", $"{count} 条笔记将被永久删除。此操作无法撤销。");

    // --- Bandeja del sistema ------------------------------------------------------------------------

    public static string TrayNewNote => T("New note", "Nueva nota",
        "Neue Notiz", "Nouvelle note", "Nova nota", "Nuova nota", "新しいノート", "新笔记");
    public static string TrayManageNotes => T("Manage notes…", "Gestionar notas…",
        "Notizen verwalten…", "Gérer les notes…", "Gerenciar notas…", "Gestisci le note…", "ノートを管理…", "管理笔记…");
    public static string TraySettings => T("Settings…", "Ajustes…",
        "Einstellungen…", "Paramètres…", "Configurações…", "Impostazioni…", "設定…", "设置…");
    public static string TrayExit => T("Exit", "Salir",
        "Beenden", "Quitter", "Sair", "Esci", "終了", "退出");

    // --- Ventana de Ajustes -------------------------------------------------------------------------

    public static string SettingsWindowTitle => T("Aldune settings", "Ajustes de Aldune",
        "Aldune-Einstellungen", "Paramètres d'Aldune", "Configurações do Aldune", "Impostazioni di Aldune", "Aldune の設定", "Aldune 设置");
    public static string SettingsHeader => T("Settings", "Ajustes",
        "Einstellungen", "Paramètres", "Configurações", "Impostazioni", "設定", "设置");
    public static string InterfaceModeSectionTitle => T("Interface mode", "Modo de interfaz",
        "Oberflächenmodus", "Mode d'interface", "Modo de interface", "Modalità dell'interfaccia", "インターフェースのモード", "界面模式");
    public static string InterfaceModeHint => T(
        "The simplified mode keeps the essentials and hides advanced options. You can switch back at any time.",
        "El modo simplificado conserva lo esencial y oculta las opciones avanzadas. Puedes volver al modo completo cuando quieras.",
        "Der vereinfachte Modus behält das Wesentliche und blendet erweiterte Optionen aus. Du kannst jederzeit zurückkehren.",
        "Le mode simplifié conserve l'essentiel et masque les options avancées. Vous pouvez revenir au mode complet à tout moment.",
        "O modo simplificado mantém o essencial e oculta as opções avançadas. Você pode voltar ao modo completo a qualquer momento.", "La modalità semplificata mantiene l'essenziale e nasconde le opzioni avanzate. Puoi tornare indietro in qualsiasi momento.", "シンプルモードでは必要な機能だけを残し、詳細なオプションを隠します。いつでも元に戻せます。", "简洁模式保留基本功能并隐藏高级选项。您可以随时切换回来。");
    public static string SwitchToSimplifiedMode => T("Use simplified mode", "Usar modo simplificado",
        "Vereinfachten Modus verwenden", "Utiliser le mode simplifié", "Usar modo simplificado", "Usa la modalità semplificata", "シンプルモードを使う", "使用简洁模式");
    public static string SwitchToCompleteMode => T("Use complete mode", "Usar modo completo",
        "Vollständigen Modus verwenden", "Utiliser le mode complet", "Usar modo completo", "Usa la modalità completa", "フルモードを使う", "使用完整模式");
    public static string ApplicationSectionTitle => T("Application", "Aplicación",
        "Anwendung", "Application", "Aplicativo", "Applicazione", "アプリケーション", "应用程序");
    public static string RestartApplicationButton => T("Restart Aldune", "Reiniciar Aldune",
        "Aldune neu starten", "Redémarrer Aldune", "Reiniciar Aldune", "Riavvia Aldune", "Aldune を再起動", "重新启动 Aldune");
    public static string RestartAppTooltip => T("Restart Aldune", "Reiniciar Aldune",
        "Aldune neu starten", "Redémarrer Aldune", "Reiniciar Aldune", "Riavvia Aldune", "Aldune を再起動", "重新启动 Aldune");
    public static string ExitApplicationButton => T("Exit Aldune", "Salir de Aldune",
        "Aldune beenden", "Quitter Aldune", "Fechar Aldune", "Esci da Aldune", "Aldune を終了", "退出 Aldune");

    public static string StartupCheckbox => T("Open Aldune at sign-in", "Abrir Aldune al iniciar sesión",
        "Aldune bei der Anmeldung öffnen", "Ouvrir Aldune à la connexion", "Abrir Aldune ao fazer login", "Apri Aldune all'accesso", "サインイン時に Aldune を起動する", "登录时打开 Aldune");
    public static string StartupHint => T("You can also remove it from Task Manager, in the Startup tab.", "También puedes quitarlo desde Administrador de tareas, en la pestaña Inicio.",
        "Du kannst es auch über den Task-Manager entfernen, auf der Registerkarte Autostart.",
        "Vous pouvez aussi le supprimer depuis le Gestionnaire des tâches, dans l'onglet Démarrage.",
        "Você também pode removê-lo pelo Gerenciador de Tarefas, na guia Inicializar.", "Puoi anche rimuoverlo da Gestione attività, nella scheda Avvio.", "タスク マネージャーの [スタートアップ] タブから無効にすることもできます。", "您也可以在任务管理器的“启动”选项卡中将其移除。");

    public static string TextScaleTitle => T("Text size", "Tamaño del texto", "Textgröße", "Taille du texte", "Tamanho do texto", "Dimensione del testo", "文字サイズ", "文字大小");
    public static string TextScaleHint => T(
        "Makes the text of the app and of your notes larger. In a note, Ctrl + mouse wheel (or Ctrl + plus / minus, Ctrl + 0 to reset) zooms just that note.",
        "Agranda el texto de la aplicación y de tus notas. En una nota, Ctrl + rueda del ratón (o Ctrl + más / menos, Ctrl + 0 para volver) amplía solo esa nota.",
        "Vergrößert den Text der App und deiner Notizen. In einer Notiz zoomt Strg + Mausrad (oder Strg + Plus / Minus, Strg + 0 zum Zurücksetzen) nur diese Notiz.",
        "Agrandit le texte de l'application et de vos notes. Dans une note, Ctrl + molette (ou Ctrl + plus / moins, Ctrl + 0 pour revenir) n'agrandit que cette note.",
        "Aumenta o texto do aplicativo e das suas notas. Em uma nota, Ctrl + roda do mouse (ou Ctrl + mais / menos, Ctrl + 0 para voltar) amplia só essa nota.", "Ingrandisce il testo dell'app e delle tue note. In una nota, Ctrl + rotellina del mouse (oppure Ctrl + più / meno, Ctrl + 0 per reimpostare) ingrandisce solo quella nota.", "アプリとノートの文字を大きくします。ノートの中では、Ctrl + マウスホイール（または Ctrl + プラス / マイナス、Ctrl + 0 でリセット）でそのノートだけを拡大縮小できます。", "放大应用和笔记中的文字。在笔记中，按住 Ctrl 滚动鼠标滚轮（或 Ctrl + 加号 / 减号，Ctrl + 0 重置）只缩放该条笔记。");
    public static string RecentNoteHotkeyCheckbox => T("Open the last edited note with a keyboard shortcut",
        "Abrir la última nota editada con un atajo de teclado",
        "Zuletzt bearbeitete Notiz mit Tastenkombination öffnen",
        "Ouvrir la dernière note modifiée avec un raccourci clavier",
        "Abrir a última nota editada com um atalho de teclado", "Apri l'ultima nota modificata con una scorciatoia da tastiera", "キーボードショートカットで最後に編集したノートを開く", "用键盘快捷键打开最近编辑的笔记");

    public static string ManagerHotkeyCheckbox => T("Open the notes list with a keyboard shortcut",
        "Abrir la lista de notas con un atajo de teclado",
        "Notizenliste mit Tastenkombination öffnen",
        "Ouvrir la liste des notes avec un raccourci clavier",
        "Abrir a lista de notas com um atalho de teclado", "Apri l'elenco delle note con una scorciatoia da tastiera", "キーボードショートカットでノート一覧を開く", "用键盘快捷键打开笔记列表");

    public static string HotkeyCheckbox => T("Create a note with a keyboard shortcut", "Crear una nota con un atajo de teclado",
        "Notiz mit Tastenkombination erstellen",
        "Créer une note avec un raccourci clavier",
        "Criar uma nota com atalho de teclado", "Crea una nota con una scorciatoia da tastiera", "キーボードショートカットでノートを作成する", "用键盘快捷键创建笔记");
    public static string HotkeyResetButton => T("Reset", "Restablecer",
        "Zurücksetzen", "Réinitialiser", "Redefinir", "Reimposta", "リセット", "重置");
    public static string HotkeyRecording => T("Press a key combination… (Esc to cancel)", "Pulsa una combinación… (Esc para cancelar)",
        "Tastenkombination drücken… (Esc zum Abbrechen)",
        "Appuyez sur une combinaison… (Esc pour annuler)",
        "Pressione uma combinação… (Esc para cancelar)", "Premi una combinazione di tasti… (Esc per annullare)", "キーの組み合わせを押してください… (Esc でキャンセル)", "请按下组合键…（按 Esc 取消）");
    public static string HotkeyNeedsModifier => T("Add at least Ctrl, Alt, Shift or Win to the combination.", "Añade al menos Ctrl, Alt, Shift o Win a la combinación.",
        "Füge mindestens Ctrl, Alt, Shift oder Win zur Kombination hinzu.",
        "Ajoutez au moins Ctrl, Alt, Shift ou Win à la combinaison.",
        "Adicione pelo menos Ctrl, Alt, Shift ou Win à combinação.", "Aggiungi almeno Ctrl, Alt, Maiusc o Win alla combinazione.", "組み合わせに Ctrl、Alt、Shift、Win のいずれかを少なくとも 1 つ加えてください。", "请在组合键中至少加入 Ctrl、Alt、Shift 或 Win 之一。");
    public static string HotkeyDisabled => T("Disabled.", "Desactivado.",
        "Deaktiviert.", "Désactivé.", "Desativado.", "Disattivata.", "無効です。", "已禁用。");
    public static string HotkeyWorks => T("Works from any application, without going to the screen edge.", "Funciona desde cualquier aplicación, sin tener que ir al borde de la pantalla.",
        "Funktioniert aus jeder Anwendung heraus, ohne zum Bildschirmrand gehen zu müssen.",
        "Fonctionne depuis n'importe quelle application, sans aller au bord de l'écran.",
        "Funciona de qualquer aplicativo, sem precisar ir até a borda da tela.", "Funziona da qualsiasi applicazione, senza andare al bordo dello schermo.", "画面の端に行かなくても、どのアプリケーションからでも動作します。", "在任何应用程序中都可使用，无需移到屏幕边缘。");
    public static string HotkeyConflict(string combo) =>
        T($"Couldn't activate it: another application already uses {combo}. Choose a different combination.",
          $"No se ha podido activar: otra aplicación ya usa {combo}. Elige otra combinación.",
            $"Konnte nicht aktiviert werden: Eine andere Anwendung verwendet bereits {combo}. Wähle eine andere Kombination.",
            $"Impossible d'activer : une autre application utilise déjà {combo}. Choisissez une autre combinaison.",
            $"Não foi possível ativar: outro aplicativo já usa {combo}. Escolha uma combinação diferente.", $"Impossibile attivarla: un'altra applicazione usa già {combo}. Scegli una combinazione diversa.", $"有効にできませんでした。別のアプリケーションがすでに {combo} を使っています。別の組み合わせを選んでください。", $"无法启用：另一个应用程序已在使用 {combo}。请选择其他组合键。");

    public static string MonitorSectionTitle => T("Screens where Aldune shows", "Pantallas donde mostrar Aldune",
        "Bildschirme, auf denen Aldune angezeigt wird",
        "Écrans où Aldune s'affiche",
        "Telas onde o Aldune é exibido", "Schermi in cui appare Aldune", "Aldune を表示する画面", "Aldune 显示的屏幕");
    public static string MonitorSectionHint => T("Choose whether to show Aldune's dock on every screen or restrict it to one.", "Elige si deseas ver el dock de Aldune en todas las pantallas o restringirlo a una específica.",
        "Wähle, ob das Aldune-Dock auf jedem Bildschirm angezeigt oder auf einen bestimmten beschränkt werden soll.",
        "Choisissez si vous souhaitez afficher le dock Aldune sur chaque écran ou le limiter à un seul.",
        "Escolha se deseja mostrar o dock do Aldune em todas as telas ou restringi-lo a uma específica.", "Scegli se mostrare il dock di Aldune su ogni schermo o limitarlo a uno.", "Aldune のドックをすべての画面に表示するか、1 つの画面だけに限定するかを選びます。", "选择是在每块屏幕上显示 Aldune 的程序坞，还是仅限于其中一块。");
    public static string AllScreens => T("On every connected screen", "En todas las pantallas conectadas",
        "Auf jedem angeschlossenen Bildschirm", "Sur chaque écran connecté", "Em todas as telas conectadas", "Su ogni schermo collegato", "接続されているすべての画面", "在所有已连接的屏幕上");
    public static string MouseScreen => T("On the screen with the mouse", "En la pantalla donde esté el ratón",
        "Auf dem Bildschirm mit der Maus",
        "Sur l'écran où se trouve la souris",
        "Na tela onde estiver o mouse", "Sullo schermo con il mouse", "マウスがある画面", "在鼠标所在的屏幕上");
    public static string ScreenLabel(int number, bool isPrimary, int width, int height)
    {
        var primary = isPrimary ? T(" · Primary", " · Principal",
            " · Primär", " · Principal", " · Principal", " · Principale", " · プライマリ", " · 主屏幕") : "";
        return T($"Screen {number}{primary} ({width}×{height})", $"Pantalla {number}{primary} ({width}×{height})",
            $"Bildschirm {number}{primary} ({width}×{height})",
            $"Écran {number}{primary} ({width}×{height})",
            $"Tela {number}{primary} ({width}×{height})", $"Schermo {number}{primary} ({width}×{height})", $"画面 {number}{primary} ({width}×{height})", $"屏幕 {number}{primary} ({width}×{height})");
    }

    public static string HideOnFullscreenCheckbox => T("Hide dock in fullscreen", "Ocultar dock a pantalla completa",
        "Dock im Vollbildmodus ausblenden", "Masquer le dock en plein écran", "Ocultar dock em tela cheia", "Nascondi il dock a schermo intero", "全画面表示中はドックを隠す", "全屏时隐藏程序坞");
    public static string HideOnFullscreenHint => T("Hides the dock automatically over any real fullscreen window — games, videos, presentations. Doesn't affect regular maximized windows (like a browser with tabs).", "Oculta el dock automáticamente ante cualquier ventana a pantalla completa de verdad — juegos, vídeos, presentaciones. No afecta a ventanas normales maximizadas (como el navegador con pestañas).",
        "Blendet das Dock automatisch über jedes echte Vollbildfenster aus — Spiele, Videos, Präsentationen. Beeinflusst keine normalen maximierten Fenster (wie ein Browser mit Tabs).",
        "Masque le dock automatiquement devant toute fenêtre en plein écran réel — jeux, vidéos, présentations. N'affecte pas les fenêtres normales agrandies (comme un navigateur avec des onglets).",
        "Oculta o dock automaticamente sobre qualquer janela em tela cheia real — jogos, vídeos, apresentações. Não afeta janelas normais maximizadas (como um navegador com abas).", "Nasconde automaticamente il dock sopra qualsiasi finestra a schermo intero vero — giochi, video, presentazioni. Non riguarda le normali finestre ingrandite (come un browser con schede).", "ゲーム、動画、プレゼンテーションなど、本物の全画面ウィンドウの上ではドックを自動的に隠します。通常の最大化したウィンドウ（タブ付きブラウザーなど）には影響しません。", "在任何真正的全屏窗口（游戏、视频、演示文稿）上自动隐藏程序坞。不影响普通的最大化窗口（例如带标签页的浏览器）。");
    public static string ShowNotePreviewCheckbox => T("Show a preview of the text", "Mostrar una vista previa del texto",
        "Textvorschau anzeigen", "Afficher un aperçu du texte", "Mostrar uma prévia do texto", "Mostra un'anteprima del testo", "テキストのプレビューを表示", "显示文字预览");
    public static string ShowNotePreviewHint => T("A second line with the start of each note, in the dock tabs and in Manage notes. Turned off, only titles are shown.", "Una segunda línea con el principio de cada nota, en las pestañas del dock y en Gestionar notas. Si la desactivas, solo se ven los títulos.",
        "Eine zweite Zeile mit dem Anfang jeder Notiz, in den Dock-Tabs und in Notizen verwalten. Deaktiviert werden nur Titel angezeigt.",
        "Une deuxième ligne avec le début de chaque note, dans les onglets du dock et dans Gérer les notes. Désactivé, seuls les titres sont affichés.",
        "Uma segunda linha com o início de cada nota, nas abas do dock e em Gerenciar notas. Desativado, apenas os títulos são exibidos.", "Una seconda riga con l'inizio di ogni nota, nelle schede del dock e in Gestisci le note. Se è disattivata, vengono mostrati solo i titoli.", "ドックのタブと「ノートを管理」で、各ノートの冒頭を 2 行目に表示します。オフの場合はタイトルだけが表示されます。", "在程序坞标签页和“管理笔记”中，为每条笔记显示开头文字的第二行。关闭后只显示标题。");
    public static string KeepDockOpenCheckbox => T("Keep dock open", "Mantener el dock abierto",
        "Dock offen halten", "Garder le dock ouvert", "Manter o dock aberto", "Mantieni il dock aperto", "ドックを開いたままにする", "保持程序坞展开");
    public static string KeepDockOpenHint => T("Keeps the dock expanded until you turn this off.", "Mantiene el dock desplegado hasta que desactives esta opción.",
        "Hält das Dock ausgeklappt, bis du diese Option deaktivierst.",
        "Garde le dock déployé jusqu'à ce que vous désactiviez cette option.",
        "Mantém o dock expandido até que você desative esta opção.", "Mantiene il dock espanso finché non lo disattivi.", "オフにするまで、ドックを展開したままにします。", "在您关闭此选项之前，程序坞保持展开。");

    public static string EdgeSectionTitle => T("Screen edge", "Lado de la pantalla",
        "Bildschirmseite", "Bord de l'écran", "Lado da tela", "Bordo dello schermo", "画面の端", "屏幕边缘");
    public static string EdgeSectionHint => T("Which edge the Aldune dock lives on.", "En qué borde vive el dock de Aldune.",
        "An welchem Rand das Aldune-Dock positioniert ist.",
        "Sur quel bord se trouve le dock Aldune.",
        "Em qual borda o dock do Aldune fica posicionado.", "Su quale bordo vive il dock di Aldune.", "Aldune のドックを置く画面の端。", "Aldune 程序坞所在的屏幕边缘。");
    public static string AlignSectionTitle => T("Position along the edge", "Posición a lo largo del borde",
        "Position entlang des Rands", "Position le long du bord", "Posição ao longo da borda", "Posizione lungo il bordo", "端に沿った位置", "沿边缘的位置");
    public static string AlignCenter => T("Centered", "Centrado",
        "Zentriert", "Centré", "Centralizado", "Al centro", "中央", "居中");
    public static string EdgeRight => T("Right", "Derecha",
        "Rechts", "Droite", "Direita", "Destra", "右", "右");
    public static string EdgeLeft => T("Left", "Izquierda",
        "Links", "Gauche", "Esquerda", "Sinistra", "左", "左");
    public static string EdgeTop => T("Top", "Arriba",
        "Oben", "Haut", "Cima", "In alto", "上", "上");
    public static string EdgeBottom => T("Bottom", "Abajo",
        "Unten", "Bas", "Baixo", "In basso", "下", "下");

    public static string RememberPositionsCheckbox => T("Remember note positions", "Recordar la posición de las notas",
        "Notizpositionen merken", "Mémoriser les positions des notes", "Lembrar a posição das notas", "Ricorda le posizioni delle note", "ノートの位置を記憶する", "记住笔记位置");
    public static string RememberPositionsHint => T("A note reopens where you left it on the desktop, like a real sticky note. Off, it opens next to the dock.", "Una nota se abre donde la dejaste en el escritorio, como un post-it de verdad. Apagado, se abre junto al dock.",
        "Eine Notiz öffnet sich dort, wo du sie auf dem Desktop gelassen hast, wie ein echter Haftzettel. Ausgeschaltet öffnet sie sich neben dem Dock.",
        "Une note se rouvre là où vous l'avez laissée sur le bureau, comme un vrai Post-it. Désactivé, elle s'ouvre près du dock.",
        "A nota reabre onde você a deixou na área de trabalho, como um post-it de verdade. Desligado, ela abre junto ao dock.", "Una nota si riapre dove l'hai lasciata sul desktop, come un vero foglietto adesivo. Se è disattivato, si apre accanto al dock.", "本物の付箋のように、ノートはデスクトップで最後に置いた場所に再び開きます。オフの場合は、ドックの横に開きます。", "笔记会像真正的便利贴一样，在桌面上您放置的位置重新打开。关闭后，则在程序坞旁边打开。");
    public static string RememberSizesCheckbox => T("Remember note sizes", "Recordar el tamaño de las notas",
        "Notizgrößen merken", "Mémoriser la taille des notes", "Lembrar o tamanho das notas", "Ricorda le dimensioni delle note", "ノートのサイズを記憶する", "记住笔记大小");
    public static string RememberSizesHint => T("A note reopens with the size you gave it. Off, it opens at the standard size and grows with its text.", "Una nota se abre con el tamaño que le diste. Apagado, se abre con el tamaño estándar y crece con su texto.",
        "Eine Notiz öffnet sich in der Größe, die du ihr gegeben hast. Ausgeschaltet öffnet sie sich in der Standardgröße und wächst mit ihrem Text.",
        "Une note se rouvre à la taille que vous lui avez donnée. Désactivé, elle s'ouvre à la taille standard et grandit avec son texte.",
        "A nota reabre com o tamanho que você deu. Desligado, ela abre no tamanho padrão e cresce com o texto.", "Una nota si riapre con la dimensione che le hai dato. Se è disattivato, si apre con la dimensione standard e cresce con il testo.", "ノートは指定したサイズで再び開きます。オフの場合は標準サイズで開き、テキストに合わせて大きくなります。", "笔记会以您设定的大小重新打开。关闭后，以标准大小打开，并随文字增长。");
    public static string LargeCheckboxesCheckbox => T("Larger task checkboxes", "Casillas de tarea más grandes",
        "Größere Aufgabenkästchen",
        "Cases de tâche plus grandes",
        "Caixas de tarefa maiores", "Caselle delle attività più grandi", "タスクのチェックボックスを大きくする", "更大的任务复选框");
    public static string LargeCheckboxesHint => T("Makes the boxes and their click area bigger, so they are easier to hit.", "Agranda las casillas y su zona de clic para acertarlas con más facilidad.",
        "Vergrößert die Kästchen und ihren Klickbereich, damit sie leichter zu treffen sind.",
        "Agrandit les cases et leur zone de clic pour les atteindre plus facilement.",
        "Aumenta as caixas e a área de clique, para acertá-las com mais facilidade.", "Ingrandisce le caselle e la loro area di clic, così sono più facili da colpire.", "ボックスとクリック範囲を大きくして、押しやすくします。", "放大复选框及其点击区域，更容易点中。");
    public static string MoveCompletedTasksCheckbox => T("Move checked tasks to the end of the list", "Mover las tareas hechas al final de la lista",
        "Erledigte Aufgaben ans Ende der Liste verschieben",
        "Déplacer les tâches cochées en bas de la liste",
        "Mover tarefas concluídas para o final da lista", "Sposta le attività completate in fondo all'elenco", "チェックしたタスクをリストの末尾に移動", "将已勾选的任务移到列表末尾");
    public static string MoveCompletedTasksHint => T("What's left stays together at the top, like a shopping list. Unchecking one moves it back up.", "Lo pendiente queda junto arriba, como en una lista de la compra. Al desmarcar una, vuelve a subir.",
        "Das Ausstehende bleibt zusammen oben, wie bei einer Einkaufsliste. Wenn du eine Aufgabe abbrichst, kommt sie wieder nach oben.",
        "Ce qui reste à faire reste groupé en haut, comme une liste de courses. Décocher une tâche la fait remonter.",
        "O que falta fica agrupado no topo, como uma lista de compras. Desmarcar uma tarefa a move de volta para cima.", "Quello che resta rimane unito in alto, come una lista della spesa. Se ne deselezioni una, torna su.", "残りのタスクが買い物リストのように上にまとまります。チェックを外すと上に戻ります。", "剩下的任务会像购物清单一样集中在顶部。取消勾选后会移回上面。");
    public static string AutoHideCompletedTasksCheckbox => T("Delete completed tasks automatically", "Borrar automáticamente las tareas completadas",
        "Erledigte Aufgaben automatisch löschen",
        "Supprimer automatiquement les tâches terminées",
        "Excluir tarefas concluídas automaticamente", "Elimina automaticamente le attività completate", "完了したタスクを自動的に削除", "自动删除已完成的任务");
    public static string AutoHideCompletedTasksHint => T("A checked task stays for a while so you can still see it, then its line is removed from the note for good.", "Una tarea marcada se queda un tiempo por si quieres verla, y después su línea se borra de la nota para siempre.",
        "Eine erledigte Aufgabe bleibt eine Weile sichtbar, dann wird ihre Zeile endgültig aus der Notiz entfernt.",
        "Une tâche cochée reste visible un moment pour que vous puissiez encore la voir, puis sa ligne est supprimée définitivement de la note.",
        "Uma tarefa marcada fica visível por um tempo para que você ainda possa vê-la, e depois sua linha é removida permanentemente da nota.", "Un'attività completata resta per un po' così puoi ancora vederla, poi la sua riga viene rimossa definitivamente dalla nota.", "チェックしたタスクはしばらく表示されたままになり、その後その行がノートから完全に削除されます。", "已勾选的任务会保留一段时间，方便您查看，之后其所在行会从笔记中永久删除。");
    public static string AutoHideCompletedTasksDelayLabel => T("After:", "Después de:",
        "Danach:", "Après :", "Depois de:", "Dopo:", "経過時間:", "经过：");
    public static string TaskDelayMinutesUnit => T("minutes", "minutos",
        "Minuten", "minutes", "minutos", "minuti", "分", "分钟");
    public static string TaskDelayHoursUnit => T("hours", "horas",
        "Stunden", "heures", "horas", "ore", "時間", "小时");
    public static string TaskDelayDaysUnit => T("days", "días",
        "Tage", "jours", "dias", "giorni", "日", "天");
    public static string TaskDelayWeeksUnit => T("weeks", "semanas",
        "Wochen", "semaines", "semanas", "settimane", "週間", "周");

    public static string TrashRetentionTitle => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira", "Cestino", "ごみ箱", "回收站");
    public static string TrashRetentionHint => T("A trashed note is deleted for good after this many days.", "Una nota en la papelera se borra para siempre pasados estos días.",
        "Eine Notiz im Papierkorb wird nach dieser Anzahl von Tagen endgültig gelöscht.",
        "Une note dans la corbeille est supprimée définitivement après ce nombre de jours.",
        "Uma nota na lixeira é excluída permanentemente após esses dias.", "Una nota nel cestino viene eliminata definitivamente dopo questo numero di giorni.", "ごみ箱に入れたノートは、この日数が過ぎると完全に削除されます。", "放入回收站的笔记会在这么多天后被永久删除。");
    public static string TrashRetentionDaysUnit => T("days", "días",
        "Tage", "jours", "dias", "giorni", "日", "天");

    public static string SyncSectionTitle => T("Sync between devices", "Sincronización entre dispositivos",
        "Geräteübergreifende Synchronisierung",
        "Synchronisation entre appareils",
        "Sincronização entre dispositivos", "Sincronizzazione tra dispositivi", "デバイス間の同期", "设备间同步");
    public static string SyncSectionHint => T(
        "Notes stay encrypted. Use a shared folder, your own Aldune server, or WebDAV.",
        "Las notas permanecen cifradas. Usa una carpeta compartida o tu propio servidor de sincronización de Aldune.",
        "Notizen bleiben verschlüsselt. Verwende einen freigegebenen Ordner, deinen eigenen Aldune-Server oder WebDAV.",
        "Les notes restent chiffrées. Utilisez un dossier partagé, votre propre serveur Aldune ou WebDAV.",
        "As notas permanecem criptografadas. Use uma pasta compartilhada, seu próprio servidor Aldune ou WebDAV.", "Le note restano cifrate. Usa una cartella condivisa, un tuo server Aldune o WebDAV.", "ノートは暗号化されたままです。共有フォルダー、自前の Aldune サーバー、または WebDAV を使えます。", "笔记始终保持加密。可使用共享文件夹、您自己的 Aldune 服务器或 WebDAV。");
    public static string SyncEnabledCheckbox => T("Sync this profile (all enabled profiles sync together)", "Sincronizar este perfil (todos los activados se sincronizan a la vez)",
        "Dieses Profil synchronisieren (alle aktivierten Profile werden gemeinsam synchronisiert)",
        "Synchroniser ce profil (tous les profils activés se synchronisent ensemble)",
        "Sincronizar este perfil (todos os ativados sincronizam juntos)", "Sincronizza questo profilo (tutti i profili attivi si sincronizzano insieme)", "このプロファイルを同期する（有効なプロファイルはすべて一緒に同期されます）", "同步此配置（所有已启用的配置会一起同步）");
    public static string SyncFolderOption => T("Shared folder / NAS", "Carpeta compartida / NAS",
        "Freigegebener Ordner / NAS", "Dossier partagé / NAS", "Pasta compartilhada / NAS", "Cartella condivisa / NAS", "共有フォルダー / NAS", "共享文件夹 / NAS");
    public static string SyncServerOption => T("Self-hosted server", "Servidor propio",
        "Eigener Server", "Serveur auto-hébergé", "Servidor próprio", "Server autogestito", "セルフホストのサーバー", "自托管服务器");
    public static string SyncWebDavOption => T("WebDAV / Nextcloud", "WebDAV / Nextcloud",
        "WebDAV / Nextcloud", "WebDAV / Nextcloud", "WebDAV / Nextcloud", "WebDAV / Nextcloud", "WebDAV / Nextcloud", "WebDAV / Nextcloud");
    public static string SyncFolderLabel => T("Folder:", "Carpeta:",
        "Ordner:", "Dossier :", "Pasta:", "Cartella:", "フォルダー:", "文件夹：");
    public static string SyncServerUrlLabel => T("Server URL:", "URL del servidor:",
        "Server-URL:", "URL du serveur :", "URL do servidor:", "URL del server:", "サーバーの URL:", "服务器 URL：");
    public static string SyncServerTokenLabel => T("Access token:", "Token de acceso:",
        "Zugriffstoken:", "Jeton d'accès :", "Token de acesso:", "Token di accesso:", "アクセストークン:", "访问令牌：");
    public static string SyncWebDavUsernameLabel => T("Username:", "Usuario:",
        "Benutzername:", "Nom d'utilisateur :", "Usuário:", "Nome utente:", "ユーザー名:", "用户名：");
    public static string SyncWebDavPasswordLabel => T("Password:", "Contraseña:",
        "Passwort:", "Mot de passe :", "Senha:", "Password:", "パスワード:", "密码：");
    public static string SyncWebDavHint => T(
        "Use an app password when your provider supports it. Aldune stores it protected on this device.",
        "Usa una contraseña de aplicación si tu proveedor la admite. Aldune la protege en este dispositivo.",
        "Verwende ein App-Passwort, wenn dein Anbieter es unterstützt. Aldune speichert es geschützt auf diesem Gerät.",
        "Utilisez un mot de passe d'application si votre fournisseur le permet. Aldune le stocke de façon sécurisée sur cet appareil.",
        "Use uma senha de aplicativo se seu provedor suportar. Aldune a armazena protegida neste dispositivo.", "Usa una password per le app quando il tuo provider la supporta. Aldune la memorizza protetta su questo dispositivo.", "プロバイダーが対応している場合は、アプリ パスワードを使ってください。Aldune はこのデバイス上で保護して保存します。", "如果您的服务商支持，请使用应用专用密码。Aldune 会在此设备上以受保护的方式存储它。");
    public static string SyncBrowseButton => T("Browse…", "Examinar…",
        "Durchsuchen…", "Parcourir…", "Procurar…", "Sfoglia…", "参照…", "浏览…");
    public static string SyncCodeLabel => T("Device sync code:", "Código de sincronización:",
        "Geräte-Sync-Code:", "Code de synchronisation :", "Código de sincronização:", "Codice di sincronizzazione del dispositivo:", "デバイスの同期コード:", "设备同步码：");
    public static string SyncGenerateCodeButton => T("Generate / copy", "Generar / copiar",
        "Generieren / kopieren", "Générer / copier", "Gerar / copiar", "Genera / copia", "生成 / コピー", "生成 / 复制");
    public static string SyncShareProfileButton => T("Share profile", "Compartir perfil",
        "Profil teilen", "Partager le profil", "Compartilhar perfil", "Condividi profilo", "プロファイルを共有", "分享配置");
    public static string SyncShowQrButton => T("Show QR", "Mostrar QR", "QR anzeigen", "Afficher le QR", "Mostrar QR", "Mostra QR", "QR を表示", "显示二维码");
    public static string QrWindowTitle => T("Profile QR", "QR del perfil", "Profil-QR", "QR du profil", "QR do perfil", "QR del profilo", "プロファイルの QR", "配置二维码");
    public static string QrWindowHint => T("On your phone: Aldune → Settings → Sync → Add profile → Scan QR.",
        "En el móvil: Aldune → Ajustes → Sincronización → Añadir perfil → Escanear QR.",
        "Auf dem Handy: Aldune → Einstellungen → Synchronisierung → Profil hinzufügen → QR scannen.",
        "Sur le téléphone : Aldune → Réglages → Synchronisation → Ajouter un profil → Scanner le QR.",
        "No celular: Aldune → Configurações → Sincronização → Adicionar perfil → Escanear QR.", "Sul telefono: Aldune → Impostazioni → Sincronizzazione → Aggiungi profilo → Scansiona QR.", "スマートフォンで: Aldune → 設定 → 同期 → プロファイルを追加 → QR をスキャン。", "在手机上：Aldune → 设置 → 同步 → 添加配置 → 扫描二维码。");
    public static string QrWindowWarning => T("This QR contains the key to your notes. Show it only to your own devices.",
        "Este QR lleva la clave de tus notas. Enséñalo solo a tus propios dispositivos.",
        "Dieser QR enthält den Schlüssel zu deinen Notizen. Zeige ihn nur deinen eigenen Geräten.",
        "Ce QR contient la clé de tes notes. Ne le montre qu'à tes propres appareils.",
        "Este QR contém a chave das suas notas. Mostre-o apenas aos seus próprios dispositivos.", "Questo QR contiene la chiave delle tue note. Mostralo solo ai tuoi dispositivi.", "この QR にはノートのキーが含まれています。自分のデバイスにだけ見せてください。", "此二维码包含您笔记的密钥。只能展示给您自己的设备。");
    public static string QrWindowCountdown(int seconds) => T($"Closes in {seconds} s", $"Se cierra en {seconds} s",
        $"Schließt in {seconds} s", $"Se ferme dans {seconds} s", $"Fecha em {seconds} s", $"Si chiude tra {seconds} s", $"{seconds} 秒後に閉じます", $"{seconds} 秒后关闭");
    public static string QrWindowClose => T("Close", "Cerrar", "Schließen", "Fermer", "Fechar", "Chiudi", "閉じる", "关闭");
    public static string QrTooLargeStatus => T("This profile is too large for a QR. Copy the code instead.",
        "Este perfil es demasiado grande para un QR. Copia el código.",
        "Dieses Profil ist zu groß für einen QR. Kopiere stattdessen den Code.",
        "Ce profil est trop grand pour un QR. Copie plutôt le code.",
        "Este perfil é grande demais para um QR. Copie o código.", "Questo profilo è troppo grande per un QR. Copia invece il codice.", "このプロファイルは QR にするには大きすぎます。代わりにコードをコピーしてください。", "此配置太大，无法生成二维码。请改为复制同步码。");
    public static string SyncRevokeAccessButton => T("Revoke old codes", "Revocar códigos anteriores",
        "Alte Codes widerrufen", "Révoquer les anciens codes", "Revogar códigos antigos", "Revoca i vecchi codici", "古いコードを無効にする", "作废旧同步码");
    public static string SyncRevokeAccessHint => T(
        "Replaces this link's key. Devices using an old code will need a new invitation.",
        "Cambia la clave de este vínculo. Los dispositivos con un código antiguo necesitarán una invitación nueva.",
        "Ersetzt den Schlüssel dieses Links. Geräte mit einem alten Code benötigen eine neue Einladung.",
        "Remplace la clé de ce lien. Les appareils utilisant un ancien code auront besoin d'une nouvelle invitation.",
        "Substitui a chave deste link. Dispositivos com um código antigo precisarão de um novo convite.", "Sostituisce la chiave di questo collegamento. I dispositivi che usano un vecchio codice avranno bisogno di un nuovo invito.", "このリンクのキーを置き換えます。古いコードを使っているデバイスは、新しい招待が必要になります。", "替换此链接的密钥。使用旧同步码的设备需要新的邀请。");
    public static string SyncRevokeAccessConfirm => T(
        "Replace this profile's sync key? Every device using an older code will stop syncing until it imports a new invitation.",
        "¿Cambiar la clave de este perfil? Los dispositivos que usen un código antiguo dejarán de sincronizarse hasta importar una invitación nueva.",
        "Den Sync-Schlüssel dieses Profils ersetzen? Alle Geräte mit einem alten Code werden nicht mehr synchronisiert, bis sie eine neue Einladung importieren.",
        "Remplacer la clé de synchronisation de ce profil ? Tout appareil utilisant un ancien code arrêterait de se synchroniser jusqu'à importer une nouvelle invitation.",
        "Substituir a chave de sincronização deste perfil? Todos os dispositivos com um código antigo pararão de sincronizar até importar um novo convite.", "Sostituire la chiave di sincronizzazione di questo profilo? Ogni dispositivo che usa un codice precedente smetterà di sincronizzarsi finché non importa un nuovo invito.", "このプロファイルの同期キーを置き換えますか？ 古いコードを使っているすべてのデバイスは、新しい招待をインポートするまで同期が止まります。", "要替换此配置的同步密钥吗？所有使用旧同步码的设备在导入新邀请之前都将停止同步。");
    public static string SyncRevokeAccessCompletedStatus => T(
        "Old profile codes were revoked. Generate and share a new invitation with the devices that should keep syncing.",
        "Los códigos antiguos del perfil han sido revocados. Genera y comparte una invitación nueva con los dispositivos autorizados.",
        "Alte Profilcodes wurden widerrufen. Generiere und teile eine neue Einladung mit den Geräten, die weiterhin synchronisieren sollen.",
        "Les anciens codes du profil ont été révoqués. Générez et partagez une nouvelle invitation avec les appareils autorisés.",
        "Os códigos antigos do perfil foram revogados. Gere e compartilhe um novo convite com os dispositivos que devem continuar sincronizando.", "I vecchi codici del profilo sono stati revocati. Genera e condividi un nuovo invito con i dispositivi che devono continuare a sincronizzarsi.", "古いプロファイルのコードを無効にしました。同期を続けるデバイスに、新しい招待を生成して共有してください。", "旧的配置同步码已作废。请为需要继续同步的设备生成并分享新的邀请。");
    public static string SyncShareCodeHint => T(
        "This invitation also carries the profile name and self-hosted server URL. It never contains the access token.",
        "Esta invitación también lleva el nombre del perfil y la URL del servidor propio. Nunca contiene el token de acceso.",
        "Diese Einladung enthält auch den Profilnamen und die URL des eigenen Servers. Sie enthält niemals das Zugriffstoken.",
        "Cette invitation contient aussi le nom du profil et l'URL du serveur auto-hébergé. Elle ne contient jamais le jeton d'accès.",
        "Este convite também contém o nome do perfil e a URL do servidor próprio. Nunca contém o token de acesso.", "Questo invito contiene anche il nome del profilo e l'URL del server autogestito. Non contiene mai il token di accesso.", "この招待には、プロファイル名とセルフホストのサーバー URL も含まれます。アクセストークンは含まれません。", "此邀请还包含配置名称和自托管服务器的 URL，但绝不包含访问令牌。");
    public static string SyncShareCodeCopiedStatus => T("Profile code copied to the clipboard.", "Código de perfil copiado al portapapeles.",
        "Profilcode in die Zwischenablage kopiert.",
        "Code du profil copié dans le presse-papiers.",
        "Código do perfil copiado para a área de transferência.", "Codice del profilo copiato negli appunti.", "プロファイルのコードをクリップボードにコピーしました。", "配置同步码已复制到剪贴板。");
    public static string SyncImportCodeButton => T("Import code", "Importar código",
        "Code importieren", "Importer le code", "Importar código", "Importa codice", "コードをインポート", "导入同步码");
    public static string SyncNowButton => T("Sync now", "Sincronizar ahora",
        "Jetzt synchronisieren", "Synchroniser maintenant", "Sincronizar agora", "Sincronizza ora", "今すぐ同期", "立即同步");
    public static string SyncInProgressStatus => T("Syncing…", "Sincronizando…",
        "Synchronisiere…", "Synchronisation en cours…", "Sincronizando…", "Sincronizzazione in corso…", "同期中…", "正在同步…");
    public static string SyncProfileLabel => T("Sync profile", "Perfil de sincronización",
        "Synchronisierungsprofil", "Profil de synchronisation", "Perfil de sincronização", "Profilo di sincronizzazione", "同期プロファイル", "同步配置");
    public static string SyncProfileNameLabel => T("Name:", "Nombre:",
        "Name:", "Nom :", "Nome:", "Nome:", "名前:", "名称：");
    public static string SyncNewProfile => T("New", "Nuevo",
        "Neu", "Nouveau", "Novo", "Nuovo", "新規", "新建");
    public static string SyncDeleteProfile => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir", "Elimina", "削除", "删除");
    public static string SyncProfileNewName(int number) => T($"Sync link {number}", $"Vínculo {number}",
        $"Sync-Link {number}", $"Lien de synchronisation {number}", $"Link de sincronização {number}", $"Collegamento di sincronizzazione {number}", $"同期リンク {number}", $"同步链接 {number}");
    public static string SyncProfileDeleteConfirm => T(
        "Delete this sync profile? Its local connection settings will be removed.",
        "¿Eliminar este perfil de sincronización? Se borrarán sus ajustes de conexión locales.",
        "Dieses Synchronisierungsprofil löschen? Die lokalen Verbindungseinstellungen werden entfernt.",
        "Supprimer ce profil de synchronisation ? Les paramètres de connexion locaux seront supprimés.",
        "Excluir este perfil de sincronização? As configurações de conexão locais serão removidas.", "Eliminare questo profilo di sincronizzazione? Le sue impostazioni di connessione locali verranno rimosse.", "この同期プロファイルを削除しますか？ ローカルの接続設定が削除されます。", "要删除此同步配置吗？它在本地的连接设置将被移除。");
    public static string SyncProfileLastRemaining => T(
        "Keep at least one sync profile.",
        "Debe quedar al menos un perfil de sincronización.",
        "Mindestens ein Synchronisierungsprofil muss vorhanden bleiben.",
        "Conservez au moins un profil de synchronisation.",
        "Mantenha pelo menos um perfil de sincronização.", "Mantieni almeno un profilo di sincronizzazione.", "同期プロファイルは少なくとも 1 つ残してください。", "请至少保留一个同步配置。");
    public static string SyncScopeAll => T("All notes", "Todas las notas",
        "Alle Notizen", "Toutes les notes", "Todas as notas", "Tutte le note", "すべてのノート", "所有笔记");
    public static string SyncScopeSelected => T("Selected notes", "Notas seleccionadas",
        "Ausgewählte Notizen", "Notes sélectionnées", "Notas selecionadas", "Note selezionate", "選択したノート", "选定的笔记");
    public static string SyncScopeTag => T("Notes with a tag", "Notas con una etiqueta",
        "Notizen mit einem Tag", "Notes avec une étiquette", "Notas com uma etiqueta", "Note con un'etichetta", "タグ付きのノート", "带标签的笔记");
    public static string SyncScopeHint => T(
        "Choose which notes this sync link can exchange. The selection stays local to this device.",
        "Elige qué notas puede intercambiar este vínculo. La selección se guarda solo en este dispositivo.",
        "Wähle aus, welche Notizen dieser Synchronisierungslink austauschen darf. Die Auswahl bleibt lokal auf diesem Gerät.",
        "Choisissez les notes que ce lien de synchronisation peut échanger. La sélection reste locale sur cet appareil.",
        "Escolha quais notas este link de sincronização pode trocar. A seleção fica salva apenas neste dispositivo.", "Scegli quali note può scambiare questo collegamento di sincronizzazione. La selezione resta locale su questo dispositivo.", "この同期リンクで交換するノートを選びます。選択内容はこのデバイスにのみ保存されます。", "选择此同步链接可以交换哪些笔记。选择仅保存在此设备本地。");
    public static string SyncChooseNotes => T("Choose notes…", "Elegir notas…",
        "Notizen auswählen…", "Choisir des notes…", "Escolher notas…", "Scegli le note…", "ノートを選択…", "选择笔记…");
    public static string SyncChooseTag => T("Choose tag…", "Elegir etiqueta…",
        "Tag auswählen…", "Choisir une étiquette…", "Escolher etiqueta…", "Scegli l'etichetta…", "タグを選択…", "选择标签…");
    public static string SyncSelectedCount(int count) => T(
        $"{count} selected for sync", $"{count} seleccionadas para sincronizar",
        $"{count} für die Synchronisierung ausgewählt",
        $"{count} sélectionnée(s) pour la synchronisation",
        $"{count} selecionada(s) para sincronizar", $"{count} selezionate per la sincronizzazione", $"同期対象に {count} 件を選択中", $"已选择 {count} 条用于同步");
    public static string SyncNotesWindowTitle => T("Choose notes to sync", "Elegir notas para sincronizar",
        "Notizen zum Synchronisieren auswählen",
        "Choisir les notes à synchroniser",
        "Escolher notas para sincronizar", "Scegli le note da sincronizzare", "同期するノートを選択", "选择要同步的笔记");
    public static string SyncNotesWindowHint => T(
        "Only the checked notes and their changes will use this sync link.",
        "Solo las notas marcadas y sus cambios usarán este vínculo de sincronización.",
        "Nur die markierten Notizen und ihre Änderungen verwenden diesen Synchronisierungslink.",
        "Seules les notes cochées et leurs modifications utiliseront ce lien de synchronisation.",
        "Somente as notas marcadas e suas alterações usarão este link de sincronização.", "Solo le note spuntate e le loro modifiche useranno questo collegamento di sincronizzazione.", "チェックしたノートとその変更だけが、この同期リンクを使います。", "只有勾选的笔记及其更改才会使用此同步链接。");
    public static string SyncNotesSave => T("Save selection", "Guardar selección",
        "Auswahl speichern", "Enregistrer la sélection", "Salvar seleção", "Salva la selezione", "選択を保存", "保存选择");
    public static string SyncNotesCancel => T("Cancel", "Cancelar",
        "Abbrechen", "Annuler", "Cancelar", "Annulla", "キャンセル", "取消");
    public static string SyncNotesEmpty => T("There are no notes to choose yet.", "Todavía no hay notas para elegir.",
        "Es gibt noch keine Notizen zur Auswahl.",
        "Il n'y a pas encore de notes à choisir.",
        "Ainda não há notas para escolher.", "Non ci sono ancora note tra cui scegliere.", "選択できるノートはまだありません。", "还没有可供选择的笔记。");
    public static string SyncAutomaticCheckbox => T("Sync periodically", "Sincronizar periódicamente",
        "Regelmäßig synchronisieren", "Synchroniser périodiquement", "Sincronizar periodicamente", "Sincronizza periodicamente", "定期的に同期する", "定期同步");
    public static string SyncAutomaticHint => T("Aldune checks for changes in the background while it is running.", "Aldune comprueba cambios en segundo plano mientras está abierta.",
        "Aldune prüft im Hintergrund auf Änderungen, während es läuft.",
        "Aldune vérifie les modifications en arrière-plan pendant son exécution.",
        "Aldune verifica alterações em segundo plano enquanto está aberto.", "Aldune controlla le modifiche in background mentre è in esecuzione.", "Aldune の実行中、バックグラウンドで変更を確認します。", "Aldune 运行期间会在后台检查更改。");
    public static string SyncIntervalLabel => T("Every minutes:", "Cada minutos:",
        "Alle Minuten:", "Toutes les minutes :", "A cada minutos:", "Ogni minuti:", "間隔（分）:", "间隔（分钟）：");
    public static string SyncCodeHint => T(
        "Use the same code on each device. Keep it private: it unlocks your notes.",
        "Usa el mismo código en cada dispositivo. Guárdalo en privado: permite descifrar tus notas.",
        "Verwende auf jedem Gerät denselben Code. Halte ihn privat: Er entsperrt deine Notizen.",
        "Utilisez le même code sur chaque appareil. Gardez-le privé : il déverrouille vos notes.",
        "Use o mesmo código em cada dispositivo. Mantenha-o privado: ele desbloqueia suas notas.", "Usa lo stesso codice su ogni dispositivo. Tienilo privato: sblocca le tue note.", "すべてのデバイスで同じコードを使います。コードはノートのロックを解除するので、他人に知られないようにしてください。", "请在每台设备上使用相同的同步码，并妥善保密：它能解锁您的笔记。");
    public static string SyncDisabledStatus => T("Sync is disabled.", "La sincronización está desactivada.",
        "Die Synchronisierung ist deaktiviert.",
        "La synchronisation est désactivée.",
        "A sincronização está desativada.", "La sincronizzazione è disattivata.", "同期は無効です。", "同步已禁用。");
    public static string SyncReadyStatus => T("Ready to sync.", "Lista para sincronizar.",
        "Bereit zur Synchronisierung.", "Prêt à synchroniser.", "Pronto para sincronizar.", "Pronto per la sincronizzazione.", "同期の準備ができました。", "已准备好同步。");
    public static string SyncCompletedStatus(int uploaded, int downloaded) =>
        T($"Sync complete: {uploaded} uploaded, {downloaded} downloaded.",
          $"Sincronización completada: {uploaded} subidas, {downloaded} descargadas.",
            $"Synchronisierung abgeschlossen: {uploaded} hochgeladen, {downloaded} heruntergeladen.",
            $"Synchronisation terminée : {uploaded} envoyée(s), {downloaded} reçue(s).",
            $"Sincronização concluída: {uploaded} enviada(s), {downloaded} baixada(s).", $"Sincronizzazione completata: {uploaded} caricate, {downloaded} scaricate.", $"同期が完了しました: アップロード {uploaded} 件、ダウンロード {downloaded} 件。", $"同步完成：上传 {uploaded} 项，下载 {downloaded} 项。");
    public static string SyncErrorStatus(string details) => T($"Sync error: {details}", $"Error de sincronización: {details}",
        $"Synchronisierungsfehler: {details}",
        $"Erreur de synchronisation : {details}",
        $"Erro de sincronização: {details}", $"Errore di sincronizzazione: {details}", $"同期エラー: {details}", $"同步错误：{details}");
    public static string SyncCodeCopiedStatus => T("Code copied to the clipboard.", "Código copiado al portapapeles.",
        "Code in die Zwischenablage kopiert.",
        "Code copié dans le presse-papiers.",
        "Código copiado para a área de transferência.", "Codice copiato negli appunti.", "コードをクリップボードにコピーしました。", "同步码已复制到剪贴板。");
    public static string SyncCodeImportedStatus => T(
        "Code imported. Check the connection and add the access token if this is a self-hosted server.",
        "Código importado. Comprueba la conexión y añade el token de acceso si es un servidor propio.",
        "Code importiert. Prüfe die Verbindung und füge das Zugriffstoken hinzu, falls es ein eigener Server ist.",
        "Code importé. Vérifiez la connexion et ajoutez le jeton d'accès si c'est un serveur auto-hébergé.",
        "Código importado. Verifique a conexão e adicione o token de acesso se for um servidor próprio.", "Codice importato. Controlla la connessione e aggiungi il token di accesso se è un server autogestito.", "コードをインポートしました。接続を確認し、セルフホストのサーバーの場合はアクセストークンを追加してください。", "同步码已导入。请检查连接；如果是自托管服务器，请添加访问令牌。");
    public static string SyncInvalidCode => T("That sync code is not valid.", "Ese código de sincronización no es válido.",
        "Dieser Synchronisierungscode ist nicht gültig.",
        "Ce code de synchronisation n'est pas valide.",
        "Esse código de sincronização não é válido.", "Quel codice di sincronizzazione non è valido.", "その同期コードは無効です。", "该同步码无效。");
    public static string SyncLastSyncNever => T("Last sync: never", "Última sincronización: nunca",
        "Letzte Synchronisierung: nie", "Dernière synchronisation : jamais", "Última sincronização: nunca", "Ultima sincronizzazione: mai", "最後の同期: なし", "上次同步：从未");
    public static string SyncLastSyncAt(DateTimeOffset at) =>
        T($"Last sync: {at.ToLocalTime():g}", $"Última sincronización: {at.ToLocalTime():g}",
            $"Letzte Synchronisierung: {at.ToLocalTime():g}",
            $"Dernière synchronisation : {at.ToLocalTime():g}",
            $"Última sincronização: {at.ToLocalTime():g}", $"Ultima sincronizzazione: {at.ToLocalTime():g}", $"最後の同期: {at.ToLocalTime():g}", $"上次同步：{at.ToLocalTime():g}");
    public static string SyncConflictsButton => T("Review conflicts", "Revisar conflictos",
        "Konflikte prüfen", "Vérifier les conflits", "Revisar conflitos", "Rivedi i conflitti", "競合を確認", "查看冲突");
    public static string SyncConflictsCount(int count) =>
        T($"{count} conflict(s) saved for review", $"{count} conflicto(s) guardado(s) para revisar",
            $"{count} Konflikt(e) zur Überprüfung gespeichert",
            $"{count} conflit(s) enregistré(s) à vérifier",
            $"{count} conflito(s) salvo(s) para revisar", $"{count} conflitto/i salvato/i da rivedere", $"確認待ちの競合が {count} 件あります", $"已保存 {count} 个待查看的冲突");
    public static string SyncNoConflicts => T("No saved conflicts.", "No hay conflictos guardados.",
        "Keine gespeicherten Konflikte.", "Aucun conflit enregistré.", "Nenhum conflito salvo.", "Nessun conflitto salvato.", "保存された競合はありません。", "没有已保存的冲突。");
    public static string SyncConflictTitle => T("Sync conflicts", "Conflictos de sincronización",
        "Synchronisierungskonflikte", "Conflits de synchronisation", "Conflitos de sincronização", "Conflitti di sincronizzazione", "同期の競合", "同步冲突");
    public static string SyncConflictHint => T(
        "Aldune kept the winning version active. You can restore the other version or dismiss this record.",
        "Aldune mantiene activa la versión ganadora. Puedes restaurar la otra versión o descartar este registro.",
        "Aldune hat die gewinnende Version aktiv gehalten. Du kannst die andere Version wiederherstellen oder diesen Eintrag verwerfen.",
        "Aldune a conservé la version gagnante active. Vous pouvez restaurer l'autre version ou ignorer cet enregistrement.",
        "Aldune manteve a versão vencedora ativa. Você pode restaurar a outra versão ou descartar este registro.", "Aldune ha mantenuto attiva la versione vincente. Puoi ripristinare l'altra versione o scartare questo record.", "Aldune は優先されたバージョンを有効のままにしました。もう一方のバージョンを復元するか、この記録を破棄できます。", "Aldune 已保留获胜的版本为当前版本。您可以恢复另一个版本，或忽略此记录。");
    public static string SyncConflictRestore => T("Restore this version", "Restaurar esta versión",
        "Diese Version wiederherstellen", "Restaurer cette version", "Restaurar esta versão", "Ripristina questa versione", "このバージョンを復元", "恢复此版本");
    public static string SyncConflictMerge => T("Merge both", "Combinar las dos",
        "Beide zusammenführen", "Fusionner les deux", "Combinar as duas", "Unisci entrambe", "両方を統合", "合并两者");
    public static string SyncConflictMergeTooltip => T(
        "Keeps the active version and adds the lines only the other one has. Nothing is lost; you remove what is left over.",
        "Mantiene la versión activa y añade las líneas que solo tiene la otra. No se pierde nada; tú quitas lo que sobre.",
        "Behält die aktive Version und fügt die Zeilen hinzu, die nur die andere hat. Nichts geht verloren; Überflüssiges entfernst du selbst.",
        "Conserve la version active et ajoute les lignes que seule l'autre possède. Rien n'est perdu ; vous supprimez ce qui est en trop.",
        "Mantém a versão ativa e adiciona as linhas que só a outra tem. Nada se perde; você remove o que sobrar.", "Mantiene la versione attiva e aggiunge le righe che ha solo l'altra. Non si perde nulla; rimuovi tu quello che avanza.", "有効なバージョンを残し、もう一方にだけある行を追加します。何も失われません。余分な行は自分で削除してください。", "保留当前版本，并加入只有另一个版本才有的行。不会丢失任何内容；多余的部分由您自己删除。");
    public static string SyncConflictDismiss => T("Dismiss", "Descartar",
        "Verwerfen", "Ignorer", "Descartar", "Scarta", "破棄", "忽略");
    public static string SyncConflictDismissAll => T("Dismiss all", "Descartar todo",
        "Alle verwerfen", "Tout ignorer", "Descartar tudo", "Scarta tutti", "すべて破棄", "全部忽略");
    public static string SyncConflictDismissAllConfirm(int count) => T(
        $"Dismiss all {count} conflicts? The losing versions will be discarded permanently.",
        $"¿Descartar los {count} conflictos? Las versiones perdedoras se descartarán definitivamente.",
        $"Alle {count} Konflikte verwerfen? Die unterlegenen Versionen werden dauerhaft gelöscht.",
        $"Ignorer les {count} conflits ? Les versions perdantes seront définitivement supprimées.",
        $"Descartar os {count} conflitos? As versões perdedoras serão descartadas definitivamente.", $"Scartare tutti i {count} conflitti? Le versioni perdenti verranno eliminate definitivamente.", $"{count} 件の競合をすべて破棄しますか？ 採用されなかったバージョンは完全に破棄されます。", $"要忽略全部 {count} 个冲突吗？落选的版本将被永久丢弃。");
    public static string SyncConflictDismissConfirm(string title) => T(
        $"Dismiss the conflict of \"{title}\"? The other version will be discarded permanently.",
        $"¿Descartar el conflicto de «{title}»? La otra versión se descartará definitivamente.",
        $"Den Konflikt von „{title}“ verwerfen? Die andere Version wird dauerhaft gelöscht.",
        $"Ignorer le conflit de « {title} » ? L'autre version sera définitivement supprimée.",
        $"Descartar o conflito de \"{title}\"? A outra versão será descartada definitivamente.", $"Scartare il conflitto di \"{title}\"? L'altra versione verrà eliminata definitivamente.", $"「{title}」の競合を破棄しますか？ もう一方のバージョンは完全に破棄されます。", $"要忽略“{title}”的冲突吗？另一个版本将被永久丢弃。");
    public static string SyncConflictDiffAt(int line, string losing, string winner, int others) => T(
        $"Line {line}: this version \"{losing}\" · active \"{winner}\"{OthersEn(others)}",
        $"Línea {line}: esta versión «{losing}» · activa «{winner}»{OthersEs(others)}",
        $"Zeile {line}: diese Version „{losing}“ · aktiv „{winner}“{OthersDe(others)}",
        $"Ligne {line} : cette version « {losing} » · active « {winner} »{OthersFr(others)}",
        $"Linha {line}: esta versão \"{losing}\" · ativa \"{winner}\"{OthersPt(others)}", $"Riga {line}: questa versione \"{losing}\" · attiva \"{winner}\"{OthersIt(others)}", $"{line} 行目: このバージョン「{losing}」· 有効「{winner}」{OthersJa(others)}", $"第 {line} 行：此版本“{losing}”· 当前“{winner}”{OthersZh(others)}");
    private static string OthersEn(int n) => n > 1 ? $" (+{n - 1} more differing lines)" : "";
    private static string OthersEs(int n) => n > 1 ? $" (+{n - 1} líneas más distintas)" : "";
    private static string OthersDe(int n) => n > 1 ? $" (+{n - 1} weitere abweichende Zeilen)" : "";
    private static string OthersFr(int n) => n > 1 ? $" (+{n - 1} autres lignes différentes)" : "";
    private static string OthersPt(int n) => n > 1 ? $" (+{n - 1} linhas diferentes a mais)" : "";
    private static string OthersIt(int n) => n > 1 ? $" (+{n - 1} righe diverse in più)" : "";
    private static string OthersZh(int n) => n > 1 ? $"（另有 {n - 1} 行不同）" : "";
    private static string OthersJa(int n) => n > 1 ? $"（ほか {n - 1} 行に違いがあります）" : "";
    public static string SyncConflictWinnerDeleted => T("The active version is a deletion.", "La versión activa es un borrado.",
        "Die aktive Version ist eine Löschung.", "La version active est une suppression.", "A versão ativa é uma exclusão.", "La versione attiva è un'eliminazione.", "有効なバージョンは削除です。", "当前版本是一次删除。");
    public static string SyncConflictDeleted => T("Deleted version", "Versión eliminada",
        "Gelöschte Version", "Version supprimée", "Versão excluída", "Versione eliminata", "削除されたバージョン", "已删除的版本");
    public static string SyncErrorTitle => T("Aldune sync", "Sincronización de Aldune",
        "Aldune-Synchronisierung", "Synchronisation Aldune", "Sincronização do Aldune", "Sincronizzazione di Aldune", "Aldune の同期", "Aldune 同步");
    public static string SyncUnauthorizedStatus => T(
        "The server rejected the token. Paste only the value after ALDUNE_SYNC_TOKEN=.",
        "El servidor ha rechazado el token. Pega solo el valor que aparece después de ALDUNE_SYNC_TOKEN=.",
        "Der Server hat das Token abgelehnt. Füge nur den Wert nach ALDUNE_SYNC_TOKEN= ein.",
        "Le serveur a rejeté le jeton. Collez uniquement la valeur après ALDUNE_SYNC_TOKEN=.",
        "O servidor rejeitou o token. Cole apenas o valor após ALDUNE_SYNC_TOKEN=.", "Il server ha rifiutato il token. Incolla solo il valore dopo ALDUNE_SYNC_TOKEN=.", "サーバーがトークンを拒否しました。ALDUNE_SYNC_TOKEN= の後の値だけを貼り付けてください。", "服务器拒绝了令牌。请只粘贴 ALDUNE_SYNC_TOKEN= 之后的值。");

    public static string LanguageSectionTitle => T("Language", "Idioma",
        "Sprache", "Langue", "Idioma", "Lingua", "言語", "语言");
    public static string AppearanceSectionTitle => T("Appearance", "Aspecto",
        "Erscheinungsbild", "Apparence", "Aparência", "Aspetto", "外観", "外观");
    public static string AppearanceSectionHint => T("Windows, the dock and messages. Notes keep their own colors.", "Ventanas, dock y avisos. Las notas conservan sus colores.",
        "Fenster, Dock und Meldungen. Notizen behalten ihre eigenen Farben.",
        "Fenêtres, dock et messages. Les notes conservent leurs propres couleurs.",
        "Janelas, dock e avisos. As notas mantêm suas próprias cores.", "Windows, il dock e i messaggi. Le note mantengono i propri colori.", "Windows、ドック、メッセージ。ノートは独自の色を保ちます。", "Windows、程序坞和消息。笔记保留各自的颜色。");
    public static string AppearanceDark => T("Dark", "Oscuro",
        "Dunkel", "Sombre", "Escuro", "Scuro", "ダーク", "深色");
    public static string AppearanceLight => T("Light", "Claro",
        "Hell", "Clair", "Claro", "Chiaro", "ライト", "浅色");
    public static string AppearancePastel => T("Pastel", "Pastel", "Pastell", "Pastel", "Pastel", "Pastello", "パステル", "粉彩");
    public static string AppearanceMidnight => T("Midnight", "Medianoche", "Mitternacht", "Minuit", "Meia-noite", "Mezzanotte", "ミッドナイト", "午夜");
    public static string AppearanceXpLight => T("XP Light", "XP claro", "XP hell", "XP clair", "XP claro", "XP chiaro", "XP ライト", "XP 浅色");
    public static string AppearanceXpDark => T("XP + 95 Dark", "XP + 95 oscuro", "XP + 95 dunkel", "XP + 95 sombre", "XP + 95 escuro", "XP + 95 scuro", "XP + 95 ダーク", "XP + 95 深色");
    public static string AppearanceTelecomLight => T("Telecom Light (lab)", "Telecomunicaciones claro (laboratorio)",
        "Telekom hell (Labor)", "Télécom clair (labo)", "Telecom claro (laboratório)", "Telecom chiaro (laboratorio)", "テレコム ライト (ラボ)", "Telecom 浅色（实验室）");
    public static string AppearanceTelecomDark => T("Telecom Dark (oscilloscope)", "Telecomunicaciones oscuro (osciloscopio)",
        "Telekom dunkel (Oszilloskop)", "Télécom sombre (oscilloscope)", "Telecom escuro (osciloscópio)", "Telecom scuro (oscilloscopio)", "テレコム ダーク (オシロスコープ)", "Telecom 深色（示波器）");
    public static string AppearanceBash => T("Bash", "Bash", "Bash", "Bash", "Bash", "Bash", "Bash", "Bash");
    public static string AppearanceTravelNotebookLight => T("Travel notebook, light", "Libreta de viaje, clara",
        "Reisenotizbuch, hell", "Carnet de voyage, clair", "Caderneta de viagem, clara", "Taccuino di viaggio, chiaro", "旅行ノート、ライト", "旅行笔记本，浅色");
    public static string AppearanceTravelNotebookDark => T("Travel notebook, dark", "Libreta de viaje, oscura",
        "Reisenotizbuch, dunkel", "Carnet de voyage, sombre", "Caderneta de viagem, escura", "Taccuino di viaggio, scuro", "旅行ノート、ダーク", "旅行笔记本，深色");
    public static string AppearanceFieldNotebookLight => T("Field notebook, light", "Cuaderno de campo, claro",
        "Feldbuch, hell", "Carnet de terrain, clair", "Caderno de campo, claro", "Quaderno da campo, chiaro", "フィールドノート、ライト", "野外笔记本，浅色");
    public static string AppearanceGridNotebookLight => T("Grid notebook, light", "Cuaderno cuadriculado, claro",
        "Karoheft, hell", "Cahier à carreaux, clair", "Caderno quadriculado, claro", "Quaderno a quadretti, chiaro", "方眼ノート、ライト", "方格笔记本，浅色");
    public static string AppearanceGridNotebookDark => T("Grid notebook, dark", "Cuaderno cuadriculado, oscuro",
        "Karoheft, dunkel", "Cahier à carreaux, sombre", "Caderno quadriculado, escuro", "Quaderno a quadretti, scuro", "方眼ノート、ダーク", "方格笔记本，深色");
    public static string AppearanceFieldNotebookDark => T("Field notebook, dark", "Cuaderno de campo, oscuro",
        "Feldbuch, dunkel", "Carnet de terrain, sombre", "Caderno de campo, escuro", "Quaderno da campo, scuro", "フィールドノート、ダーク", "野外笔记本，深色");
    public static string AppearancePhosphor => T("Phosphor (old monitor)", "Fósforo (monitor antiguo)",
        "Phosphor (alter Monitor)", "Phosphore (vieux moniteur)", "Fósforo (monitor antigo)", "Fosforo (vecchio monitor)", "フォスファー (古いモニター)", "荧光屏（旧显示器）");

    /// <summary>Nombre visible de cada hueco de color de un aspecto (el id viene de AspectCatalog).</summary>
    public static string AspectSlotName(string slot) => slot switch
    {
        "titleBar" => T("Title bar", "Barra de título", "Titelleiste", "Barre de titre", "Barra de título", "Barra del titolo", "タイトルバー", "标题栏"),
        "accent" => T("Accent", "Acento", "Akzent", "Accent", "Destaque", "Accento", "アクセント", "强调色"),
        "ink" => T("Ink", "Tinta", "Tinte", "Encre", "Tinta", "Inchiostro", "インク", "墨水"),
        "panel" => T("Panel", "Panel", "Bedienfeld", "Panneau", "Painel", "Pannello", "パネル", "面板"),
        "trace" => T("Trace (CH1)", "Traza (CH1)", "Spur (CH1)", "Trace (CH1)", "Traço (CH1)", "Traccia (CH1)", "トレース (CH1)", "轨迹 (CH1)"),
        "background" => T("Terminal", "Terminal", "Terminal", "Terminal", "Terminal", "Terminale", "ターミナル", "终端"),
        "user" => T("User", "Usuario", "Benutzer", "Utilisateur", "Usuário", "Utente", "ユーザー", "用户"),
        "path" => T("Path", "Ruta", "Pfad", "Chemin", "Caminho", "Percorso", "パス", "路径"),
        "phosphor" => T("Phosphor", "Fósforo", "Phosphor", "Phosphore", "Fósforo", "Fosforo", "フォスファー", "荧光"),
        "details" => T("Details", "Detalles", "Details", "Détails", "Detalhes", "Dettagli", "詳細", "详细信息"),
        "paper" => T("Paper", "Papel", "Papier", "Papier", "Papel", "Carta", "紙", "纸张"),
        _ => slot,
    };

    public static string AspectPalettePresets => T("Palette", "Paleta", "Palette", "Palette", "Paleta", "Tavolozza", "パレット", "调色板");
    public static string AspectResetColors => T("Reset colors", "Restablecer colores", "Farben zurücksetzen",
        "Réinitialiser les couleurs", "Redefinir cores", "Ripristina i colori", "色をリセット", "重置颜色");
    public static string AspectSuggestTheme(string theme) => T(
        $"Also use the “{theme}” note theme?",
        $"¿Usar también el tema de notas «{theme}»?",
        $"Auch das Notizthema „{theme}“ verwenden?",
        $"Utiliser aussi le thème de notes « {theme} » ?",
        $"Usar também o tema de notas “{theme}”?", $"Usare anche il tema di note “{theme}”?", $"ノートのテーマ「{theme}」も使いますか？", $"是否也使用“{theme}”笔记主题？");
    public static string AppearanceSystem => T("Same as Windows", "Como Windows",
        "Wie Windows", "Comme Windows", "Como Windows", "Come Windows", "Windows と同じ", "与 Windows 相同");
    public static string AppearanceUniformColor => T("Same color for every note", "Mismo color en todas las notas",
        "Gleiche Farbe für alle Notizen", "Même couleur pour toutes les notes", "Mesma cor em todas as notas", "Stesso colore per tutte le note", "すべてのノートを同じ色にする", "所有笔记使用同一颜色");
    public static string AppearanceUniformColorHint => T("Only changes how they look: each note keeps its own color.",
        "Solo cambia cómo se ven: cada nota conserva su color.",
        "Ändert nur das Aussehen: Jede Notiz behält ihre eigene Farbe.",
        "Ne change que l'apparence : chaque note garde sa couleur.",
        "Só muda a aparência: cada nota mantém a sua cor.", "Cambia solo come appaiono: ogni nota mantiene il proprio colore.", "見た目だけを変えます。各ノートは自分の色を保ちます。", "只改变外观：每条笔记保留自己的颜色。");
    public static string AppearanceSyncSignal => T("Show sync status on each note", "Señal de sincronización en cada nota",
        "Synchronisierungsstatus auf jeder Notiz", "Indicateur de synchronisation sur chaque note", "Sinal de sincronização em cada nota", "Mostra lo stato di sincronizzazione su ogni nota", "各ノートに同期の状態を表示する", "在每条笔记上显示同步状态");
    public static string AppearanceNoteColorStripe => T(
        "Note color as a side strip (not the whole note)",
        "Color de la nota solo como franja lateral (no la nota entera)",
        "Notizfarbe nur als Seitenstreifen (nicht die ganze Notiz)",
        "Couleur de la note en bande latérale (pas toute la note)",
        "Cor da nota só como faixa lateral (não a nota inteira)", "Colore della nota come striscia laterale (non su tutta la nota)", "ノートの色をサイドの帯で表示（ノート全体ではなく）", "笔记颜色显示为侧边条带（而非整条笔记）");
    public static string AppearanceNoteColorStripeLocked => T(
        "This look already decides how the note color is shown.",
        "Este aspecto ya decide cómo se muestra el color de la nota.",
        "Dieses Design bestimmt bereits, wie die Notizfarbe angezeigt wird.",
        "Cet aspect décide déjà comment la couleur de la note s'affiche.",
        "Este visual já decide como a cor da nota é exibida.", "Questo aspetto decide già come viene mostrato il colore della nota.", "このデザインでは、ノートの色の表示方法がすでに決まっています。", "此外观已决定笔记颜色的显示方式。");
    public static string AppearanceSquareCorners => T("Square corners on notes, dock and windows", "Esquinas rectas en notas, dock y ventanas",
        "Eckige Ecken bei Notizen, Dock und Fenstern", "Coins carrés pour les notes, le dock et les fenêtres", "Cantos retos nas notas, no dock e nas janelas", "Angoli squadrati su note, dock e finestre", "ノート、ドック、ウィンドウの角を四角にする", "笔记、程序坞和窗口使用方形边角");
    public static string SyncSignalLabel(Aldune.Core.SyncSignalState state) => state switch
    {
        Aldune.Core.SyncSignalState.Synced => T("synced", "sincronizada", "synchronisiert", "synchronisée", "sincronizada", "sincronizzata", "同期済み", "已同步"),
        Aldune.Core.SyncSignalState.Pending => T("pending", "pendiente", "ausstehend", "en attente", "pendente", "in attesa", "保留中", "待同步"),
        Aldune.Core.SyncSignalState.Conflict => T("conflict", "conflicto", "Konflikt", "conflit", "conflito", "conflitto", "競合", "冲突"),
        Aldune.Core.SyncSignalState.Excluded => T("not synced", "no se sincroniza", "wird nicht synchronisiert", "non synchronisée", "não sincronizada", "non sincronizzata", "未同期", "未同步"),
        _ => "",
    };
    public static string AppearanceUniformColorPick => T("Choose color…", "Elegir color…",
        "Farbe wählen…", "Choisir la couleur…", "Escolher cor…", "Scegli colore…", "色を選択…", "选择颜色…");
    public static string LanguageNameIn(string code) => code switch
    {
        "de" => T("German", "Alemán", "Deutsch", "Allemand", "Alemão", "Tedesco", "ドイツ語", "德语"),
        "en" => T("English", "Inglés", "Englisch", "Anglais", "Inglês", "Inglese", "英語", "英语"),
        "es" => T("Spanish", "Español", "Spanisch", "Espagnol", "Espanhol", "Spagnolo", "スペイン語", "西班牙语"),
        "fr" => T("French", "Francés", "Französisch", "Français", "Francês", "Francese", "フランス語", "法语"),
        "it" => T("Italian", "Italiano", "Italienisch", "Italien", "Italiano", "Italiano", "イタリア語", "意大利语"),
        "ja" => T("Japanese", "Japonés", "Japanisch", "Japonais", "Japonês", "Giapponese", "日本語", "日语"),
        "zh" => T("Chinese (Simplified)", "Chino (simplificado)", "Chinesisch (vereinfacht)", "Chinois (simplifié)", "Chinês (simplificado)", "Cinese (semplificato)", "中国語（簡体字）", "简体中文"),
        "pt" => T("Portuguese (Brazil)", "Portugués (Brasil)", "Portugiesisch (Brasilien)", "Portugais (Brésil)", "Português (Brasil)", "Portoghese (Brasile)", "ポルトガル語 (ブラジル)", "葡萄牙语（巴西）"),
        _ => code
    };
    public static string LanguageSectionHint => T("Restarting Aldune applies the change to every window.", "Reiniciar Aldune aplica el cambio en todas las ventanas.",
        "Aldune neu starten, um die Änderung in allen Fenstern zu übernehmen.",
        "Redémarrer Aldune applique la modification à toutes les fenêtres.",
        "Reiniciar o Aldune aplica a mudança em todas as janelas.", "Riavviando Aldune la modifica si applica a ogni finestra.", "Aldune を再起動すると、変更がすべてのウィンドウに反映されます。", "重新启动 Aldune 后，更改会应用到所有窗口。");

    // --- Temas -----------------------------------------------------------------------------------

    public static string ThemesSectionTitle => T("Note colors", "Colores de las notas",
        "Notizfarben", "Couleurs des notes", "Cores das notas", "Colori delle note", "ノートの色", "笔记颜色");
    public static string ThemesSectionHint => T(
        "The theme sets the colors for new notes and the quick colors in each note's menu.",
        "El tema decide el color de las notas nuevas y los colores rápidos del menú de cada nota.",
        "Das Thema legt die Farben für neue Notizen und die Schnellfarben im Menü jeder Notiz fest.",
        "Le thème définit les couleurs des nouvelles notes et les couleurs rapides dans le menu de chaque note.",
        "O tema define as cores para novas notas e as cores rápidas no menu de cada nota.", "Il tema stabilisce i colori delle nuove note e i colori rapidi nel menu di ogni nota.", "テーマが、新しいノートの色と、各ノートのメニューにあるクイックカラーを決めます。", "主题决定新笔记的颜色，以及每条笔记菜单中的快捷颜色。");
    public static string ThemeNew => T("New", "Nuevo",
        "Neu", "Nouveau", "Novo", "Nuovo", "新規", "新建");
    public static string ThemeDuplicate => T("Duplicate", "Duplicar",
        "Duplizieren", "Dupliquer", "Duplicar", "Duplica", "複製", "复制");
    public static string ThemeEdit => T("Edit", "Editar",
        "Bearbeiten", "Modifier", "Editar", "Modifica", "編集", "编辑");
    public static string ThemeDeleteButton => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir", "Elimina", "削除", "删除");
    public static string ThemeDeleteConfirm(string name) => T(
        $"Delete the theme \"{name}\"? Your notes keep their colors.",
        $"¿Eliminar el tema «{name}»? Tus notas conservan sus colores.",
        $"Das Thema \"{name}\" löschen? Deine Notizen behalten ihre Farben.",
        $"Supprimer le thème \"{name}\" ? Vos notes conservent leurs couleurs.",
        $"Excluir o tema \"{name}\"? Suas notas mantêm suas cores.", $"Eliminare il tema \"{name}\"? Le tue note mantengono i loro colori.", $"テーマ「{name}」を削除しますか？ ノートの色は変わりません。", $"要删除主题“{name}”吗？您的笔记会保留各自的颜色。");
    public static string ThemeCopyName(string name) => T($"{name} (copy)", $"{name} (copia)",
        $"{name} (Kopie)", $"{name} (copie)", $"{name} (cópia)", $"{name} (copia)", $"{name} (コピー)", $"{name}（副本）");
    public static string ThemeNewName => T("My theme", "Mi tema",
        "Mein Thema", "Mon thème", "Meu tema", "Il mio tema", "マイテーマ", "我的主题");
    public static string NewNoteToneLabel => T("New notes are", "Las notas nuevas son",
        "Neue Notizen sind", "Les nouvelles notes sont", "As novas notas são", "Le nuove note sono", "新しいノートの配色", "新笔记为");
    public static string ToneLight => T("Light", "Claras",
        "Hell", "Claires", "Claras", "Chiare", "ライト", "浅色");
    public static string ToneDark => T("Dark", "Oscuras",
        "Dunkel", "Sombres", "Escuras", "Scure", "ダーク", "深色");
    public static string ToneBoth => T("Both, alternating", "De los dos tipos, alternando",
        "Beide, abwechselnd", "Les deux, en alternance", "As duas, alternando", "Entrambe, alternate", "両方を交互に", "两者交替");
    public static string ColorAssignmentLabel => T("Color of new notes", "Color de las notas nuevas",
        "Farbe neuer Notizen", "Couleur des nouvelles notes", "Cor das novas notas", "Colore delle nuove note", "新しいノートの色", "新笔记的颜色");
    public static string AssignAvoidNeighbors => T(
        "Rotate, never the same as the one next to it (recommended)",
        "Rotar, sin repetir el de al lado (recomendado)",
        "Rotieren, nie dieselbe wie die daneben (empfohlen)",
        "En rotation, jamais la même que la voisine (recommandé)",
        "Rodar, nunca a mesma que a do lado (recomendado)", "A rotazione, mai uguale a quello accanto (consigliato)", "順番に切り替え、隣と同じ色にしない（おすすめ）", "轮换，且不与相邻的相同（推荐）");
    public static string AssignRotate => T("Rotate through the theme", "Rotar por el tema",
        "Durch das Thema rotieren", "Parcourir le thème en rotation", "Rodar pelo tema", "A rotazione nel tema", "テーマ内で順番に切り替える", "在主题中轮换");
    public static string AssignMostDistinct => T("The most different from the rest", "El más distinto del resto",
        "Die am meisten von den anderen abweichende",
        "La plus différente des autres",
        "A mais diferente das demais", "Il più diverso dagli altri", "他と最も違う色", "与其余颜色差别最大的");
    public static string AssignFixed => T("Always the same color", "Siempre el mismo color",
        "Immer dieselbe Farbe", "Toujours la même couleur", "Sempre a mesma cor", "Sempre lo stesso colore", "常に同じ色", "始终使用同一颜色");
    public static string ApplyThemeButton => T("Apply to existing notes…", "Aplicar a las notas existentes…",
        "Auf vorhandene Notizen anwenden…",
        "Appliquer aux notes existantes…",
        "Aplicar às notas existentes…", "Applica alle note esistenti…", "既存のノートに適用…", "应用到现有笔记…");
    public static string ApplyThemeConfirmTitle => T("Apply theme", "Aplicar tema",
        "Thema anwenden", "Appliquer le thème", "Aplicar tema", "Applica tema", "テーマを適用", "应用主题");
    public static string ApplyThemeConfirm(int count) => count == 1
        ? T("The color of your active note will change, even if you picked it by hand. It will sync to your other devices.",
            "Se cambiará el color de tu nota activa, aunque lo eligieras a mano. Se sincronizará a tus otros dispositivos.",
            "Die Farbe deiner aktiven Notiz wird geändert, auch wenn du sie selbst ausgewählt hast. Sie wird auf deine anderen Geräte synchronisiert.",
            "La couleur de votre note active sera modifiée, même si vous l'avez choisie manuellement. Elle sera synchronisée sur vos autres appareils.",
            "A cor da sua nota ativa será alterada, mesmo que você a tenha escolhido manualmente. Ela será sincronizada com seus outros dispositivos.", "Il colore della tua nota attiva cambierà, anche se l'hai scelto a mano. Si sincronizzerà con i tuoi altri dispositivi.", "アクティブなノートの色が変わります。手動で選んだ色も同様です。他のデバイスにも同期されます。", "您当前活动笔记的颜色将会改变，即使是您手动选择的颜色。该更改将同步到您的其他设备。")
        : T($"The color of your {count} active notes will change, including colors you picked by hand. It will sync to your other devices.",
            $"Se cambiará el color de tus {count} notas activas, incluidos los que elegiste a mano. Se sincronizará a tus otros dispositivos.",
            $"Die Farbe deiner {count} aktiven Notizen wird geändert, einschließlich der von dir selbst ausgewählten. Sie werden auf deine anderen Geräte synchronisiert.",
            $"La couleur de vos {count} notes actives sera modifiée, y compris celles que vous avez choisies manuellement. Elles seront synchronisées sur vos autres appareils.",
            $"A cor das suas {count} notas ativas será alterada, incluindo as que você escolheu manualmente. Elas serão sincronizadas com seus outros dispositivos.", $"Il colore delle tue {count} note attive cambierà, compresi i colori scelti a mano. Si sincronizzerà con i tuoi altri dispositivi.", $"{count} 件のアクティブなノートの色が変わります。手動で選んだ色も含まれます。他のデバイスにも同期されます。", $"您的 {count} 条活动笔记的颜色将会改变，包括您手动选择的颜色。该更改将同步到您的其他设备。");

    public static string ThemeEditorTitle => T("Edit theme", "Editar tema",
        "Thema bearbeiten", "Modifier le thème", "Editar tema", "Modifica tema", "テーマを編集", "编辑主题");
    public static string ThemeEditorNewTitle => T("New theme", "Nuevo tema",
        "Neues Thema", "Nouveau thème", "Novo tema", "Nuovo tema", "新しいテーマ", "新主题");
    public static string ThemeEditorNameLabel => T("Name", "Nombre",
        "Name", "Nom", "Nome", "Nome", "名前", "名称");
    public static string ThemeDarkColors => T("Dark (light text)", "Oscuros (texto claro)",
        "Dunkel (heller Text)", "Sombres (texte clair)", "Escuras (texto claro)", "Scuro (testo chiaro)", "ダーク (明るい文字)", "深色（浅色文字）");
    public static string ThemeLightColors => T("Light (dark text)", "Claros (texto oscuro)",
        "Hell (dunkler Text)", "Claires (texte foncé)", "Claras (texto escuro)", "Chiaro (testo scuro)", "ライト (暗い文字)", "浅色（深色文字）");
    public static string ThemeAddColor => T("Add color", "Añadir color",
        "Farbe hinzufügen", "Ajouter une couleur", "Adicionar cor", "Aggiungi colore", "色を追加", "添加颜色");
    public static string ThemeMoveLeft => T("Move left", "Mover a la izquierda",
        "Nach links verschieben", "Déplacer à gauche", "Mover para a esquerda", "Sposta a sinistra", "左へ移動", "左移");
    public static string ThemeMoveRight => T("Move right", "Mover a la derecha",
        "Nach rechts verschieben", "Déplacer à droite", "Mover para a direita", "Sposta a destra", "右へ移動", "右移");
    public static string ThemeRemoveColor => T("Remove", "Quitar",
        "Entfernen", "Supprimer", "Remover", "Rimuovi", "削除", "移除");
    public static string ThemeEditorEmpty => T("Add at least one color.", "Añade al menos un color.",
        "Füge mindestens eine Farbe hinzu.", "Ajoutez au moins une couleur.", "Adicione pelo menos uma cor.", "Aggiungi almeno un colore.", "色を少なくとも 1 つ追加してください。", "请至少添加一种颜色。");

    /// <summary>Los de serie se traducen; los propios se enseñan con el nombre que les dio el usuario.</summary>
    internal static string ThemeDisplayName(Aldune.Core.NoteTheme theme) => theme.Id switch
    {
        Aldune.Core.NoteThemes.ClassicId => T("Classic", "Clásico",
            "Klassisch", "Classique", "Clássico", "Classico", "クラシック", "经典"),
        Aldune.Core.NoteThemes.SereneId => T("Serene", "Sereno",
            "Ruhig", "Serein", "Sereno", "Sereno", "セレーン", "宁静"),
        Aldune.Core.NoteThemes.GrayId => T("Graphite", "Grafito",
            "Graphit", "Graphite", "Grafite", "Grafite", "グラファイト", "石墨"),
        Aldune.Core.NoteThemes.PastelId => T("Pastel", "Pastel",
            "Pastell", "Pastel", "Pastel", "Pastello", "パステル", "粉彩"),
        Aldune.Core.NoteThemes.AutumnId => T("Autumn", "Otoño",
            "Herbst", "Automne", "Outono", "Autunno", "オータム", "秋日"),
        Aldune.Core.NoteThemes.OceanId => T("Ocean", "Océano",
            "Ozean", "Océan", "Oceano", "Oceano", "オーシャン", "海洋"),
        Aldune.Core.NoteThemes.XpId => T("XP", "XP", "XP", "XP", "XP", "XP", "XP", "XP"),
        _ => theme.Name,
    };

    public static string QuickHelpTitle => T("Quick help", "Ayuda rápida",
        "Schnellhilfe", "Aide rapide", "Ajuda rápida", "Guida rapida", "クイックヘルプ", "快速帮助");
    public static string QuickHelpHover => T("• Hover over the screen edge to fan out the notes.", "• Pasa el ratón por el borde de la pantalla para desplegar las notas.",
        "• Bewege die Maus zum Bildschirmrand, um die Notizen aufzufächern.",
        "• Survolez le bord de l'écran pour déployer les notes.",
        "• Passe o mouse pela borda da tela para expandir as notas.", "• Passa il mouse sul bordo dello schermo per aprire a ventaglio le note.", "• 画面の端にマウスを重ねると、ノートが扇状に広がります。", "• 把鼠标悬停在屏幕边缘，笔记就会像扇子一样展开。");
    public static string QuickHelpDrag => T("• Click a tab to open that note; drag it yourself to move it — it will remember the spot.", "• Haz clic en una pestaña para abrir esa nota; arrástrala tú para moverla — recordará el sitio.",
        "• Klicke auf eine Registerkarte, um die Notiz zu öffnen; ziehe sie selbst, um sie zu verschieben — der Platz wird gespeichert.",
        "• Cliquez sur un onglet pour ouvrir la note ; faites-la glisser vous-même pour la déplacer — elle mémorisera la position.",
        "• Clique em uma aba para abrir a nota; arraste-a para movê-la — ela vai lembrar o lugar.", "• Fai clic su una scheda per aprire quella nota; trascinala tu per spostarla — ricorderà il punto.", "• タブをクリックするとそのノートが開きます。ドラッグして自分で動かすと、その位置を覚えます。", "• 点击标签页即可打开对应的笔记；自己拖动它来移动，它会记住位置。");
    public static string QuickHelpRightClick => T("• Right-click a tab: change color, archive, or send to trash without opening it.", "• Clic derecho en una pestaña: cambiar color, archivar o tirar a la papelera sin abrirla.",
        "• Rechtsklick auf eine Registerkarte: Farbe ändern, archivieren oder in den Papierkorb verschieben, ohne sie zu öffnen.",
        "• Clic droit sur un onglet : changer la couleur, archiver ou mettre à la corbeille sans l'ouvrir.",
        "• Clique com o botão direito em uma aba: mudar cor, arquivar ou enviar para a lixeira sem abri-la.", "• Clic destro su una scheda: cambia colore, archivia o manda nel cestino senza aprirla.", "• タブを右クリック: ノートを開かずに、色の変更、アーカイブ、ごみ箱への移動ができます。", "• 右键点击标签页：无需打开笔记，即可更改颜色、归档或移到回收站。");
    public static string SettingsPageGeneral => T("General", "General",
        "Allgemein", "Général", "Geral", "Generale", "全般", "常规");
    public static string SettingsPageAppearance => T("Appearance", "Aspecto",
        "Aussehen", "Apparence", "Aparência", "Aspetto", "外観", "外观");
    // Mismo nombre que la pestaña de Android (SETTINGS_PARITY.md).
    public static string SettingsPageNotes => T("Notes", "Notas",
        "Notizen", "Notes", "Notas", "Note", "ノート", "笔记");
    public static string SettingsPageDock => T("Dock and screens", "Dock y pantallas",
        "Dock und Bildschirme", "Dock et écrans", "Dock e telas", "Dock e schermi", "ドックと画面", "程序坞和屏幕");
    public static string SettingsPageColors => T("Colors", "Colores",
        "Farben", "Couleurs", "Cores", "Colori", "色", "颜色");
    public static string SettingsPageSync => T("Sync", "Sincronización",
        "Synchronisierung", "Synchronisation", "Sincronização", "Sincronizzazione", "同期", "同步");
    public static string SettingsPageHelp => T("Help", "Ayuda",
        "Hilfe", "Aide", "Ajuda", "Aiuto", "ヘルプ", "帮助");
    public static string SettingsPageAbout => T("About", "Acerca de",
        "Info", "À propos", "Sobre", "Informazioni", "情報", "关于");
    public static string QuickHelpKeyboardGroup => T("Without the mouse", "Sin ratón", "Ohne Maus", "Sans souris", "Sem mouse", "Senza il mouse", "マウスなしで操作", "不使用鼠标");
    public static string QuickHelpKeyboardManagerOn(string combo) =>
        T($"• {combo} opens the list of notes: Tab or the arrow keys move between notes, Enter opens one, Space selects it.",
          $"• {combo} abre la lista de notas: Tab o las flechas se mueven entre notas, Intro abre una y Espacio la marca.",
          $"• {combo} öffnet die Notizenliste: Tab oder die Pfeiltasten wechseln zwischen Notizen, Eingabe öffnet eine, Leertaste markiert sie.",
          $"• {combo} ouvre la liste des notes : Tab ou les flèches passent d'une note à l'autre, Entrée en ouvre une, Espace la sélectionne.",
          $"• {combo} abre a lista de notas: Tab ou as setas se movem entre notas, Enter abre uma e Espaço a seleciona.", $"• {combo} apre l'elenco delle note: Tab o i tasti freccia si spostano tra le note, Invio ne apre una, Spazio la seleziona.", $"• {combo} でノート一覧を開きます。Tab または矢印キーでノート間を移動し、Enter で開き、Space で選択します。", $"• {combo} 打开笔记列表：Tab 或方向键在笔记之间移动，Enter 打开一条，空格键选中它。");
    public static string QuickHelpKeyboardManagerOff => T(
        "• To open the list of notes from the keyboard, turn on “Open the notes list with a keyboard shortcut” above, or use the tray icon.",
        "• Para abrir la lista de notas con el teclado, activa arriba «Abrir la lista de notas con un atajo de teclado» o usa el icono de la bandeja.",
        "• Um die Notizenliste per Tastatur zu öffnen, aktiviere oben „Notizenliste mit Tastenkombination öffnen“ oder nutze das Taskleistensymbol.",
        "• Pour ouvrir la liste des notes au clavier, activez plus haut « Ouvrir la liste des notes avec un raccourci clavier » ou utilisez l'icône de la barre d'état.",
        "• Para abrir a lista de notas pelo teclado, ative acima «Abrir a lista de notas com um atalho de teclado» ou use o ícone da bandeja.", "• Per aprire l'elenco delle note da tastiera, attiva sopra “Apri l'elenco delle note con una scorciatoia da tastiera”, oppure usa l'icona nell'area di notifica.", "• キーボードからノート一覧を開くには、上の「キーボードショートカットでノート一覧を開く」をオンにするか、トレイのアイコンを使ってください。", "• 若要用键盘打开笔记列表，请开启上面的“用键盘快捷键打开笔记列表”，或使用托盘图标。");
    public static string QuickHelpKeyboardDock => T(
        "• Ctrl+Alt+D puts the keyboard on the dock: Up and Down move between tabs, Enter opens the note, Tab reaches the buttons below and Esc gives the keyboard back.",
        "• Ctrl+Alt+D pone el teclado en el dock: Arriba y Abajo pasan de una pestaña a otra, Intro abre la nota, Tab llega a los botones de abajo y Esc devuelve el teclado.",
        "• Strg+Alt+D legt die Tastatur auf das Dock: Pfeil nach oben und unten wechseln zwischen den Reitern, Eingabe öffnet die Notiz, Tab erreicht die Schaltflächen darunter und Esc gibt die Tastatur zurück.",
        "• Ctrl+Alt+D donne le clavier au dock : Haut et Bas passent d'un onglet à l'autre, Entrée ouvre la note, Tab atteint les boutons du bas et Échap rend le clavier.",
        "• Ctrl+Alt+D leva o teclado ao dock: Cima e Baixo passam de uma aba a outra, Enter abre a nota, Tab chega aos botões de baixo e Esc devolve o teclado.", "• Ctrl+Alt+D porta la tastiera sul dock: Su e Giù si spostano tra le schede, Invio apre la nota, Tab raggiunge i pulsanti in basso ed Esc restituisce la tastiera.", "• Ctrl+Alt+D でキーボード操作をドックに移します。上下キーでタブを移動し、Enter でノートを開き、Tab で下のボタンに移動し、Esc でキーボード操作を戻します。", "• Ctrl+Alt+D 把键盘焦点移到程序坞：上、下键在标签页之间移动，Enter 打开笔记，Tab 到达下方的按钮，Esc 归还键盘焦点。");
    public static string QuickHelpKeyboardZoom => T(
        "• In a note, Ctrl + mouse wheel (or Ctrl + plus / minus, Ctrl + 0 to reset) zooms its text. In Settings, “Text size” enlarges the whole app.",
        "• En una nota, Ctrl + rueda del ratón (o Ctrl + más / menos, Ctrl + 0 para volver) amplía su texto. En Ajustes, «Tamaño del texto» agranda toda la aplicación.",
        "• In einer Notiz zoomt Strg + Mausrad (oder Strg + Plus / Minus, Strg + 0 zum Zurücksetzen) den Text. In den Einstellungen vergrößert „Textgröße“ die ganze App.",
        "• Dans une note, Ctrl + molette (ou Ctrl + plus / moins, Ctrl + 0 pour revenir) agrandit son texte. Dans les paramètres, « Taille du texte » agrandit toute l'application.",
        "• Em uma nota, Ctrl + roda do mouse (ou Ctrl + mais / menos, Ctrl + 0 para voltar) amplia o texto. Em Ajustes, «Tamanho do texto» aumenta todo o aplicativo.", "• In una nota, Ctrl + rotellina del mouse (oppure Ctrl + più / meno, Ctrl + 0 per reimpostare) ingrandisce il suo testo. Nelle Impostazioni, “Dimensione del testo” ingrandisce tutta l'app.", "• ノートの中では、Ctrl + マウスホイール（または Ctrl + プラス / マイナス、Ctrl + 0 でリセット）でテキストを拡大縮小できます。設定の「文字サイズ」では、アプリ全体が大きくなります。", "• 在笔记中，按住 Ctrl 滚动鼠标滚轮（或 Ctrl + 加号 / 减号，Ctrl + 0 重置）可缩放其文字。在设置中，“文字大小”会放大整个应用。");
    public static string QuickHelpKeyboardFocus => T(
        "• Tab moves between buttons and a ring shows where you are; Space or Enter presses. Colors are chosen with the arrow keys and Space. With a Windows high-contrast theme, Aldune uses its colors.",
        "• Tab pasa de un botón a otro y un anillo marca dónde estás; Espacio o Intro pulsa. Los colores se eligen con las flechas y Espacio. Con un tema de alto contraste de Windows, Aldune usa sus colores.",
        "• Tab wechselt zwischen Schaltflächen, ein Ring zeigt, wo du bist; Leertaste oder Eingabe drückt. Farben wählst du mit den Pfeiltasten und der Leertaste. Mit einem Windows-Kontrastdesign nutzt Aldune dessen Farben.",
        "• Tab passe d'un bouton à l'autre et un anneau montre où vous êtes ; Espace ou Entrée appuie. Les couleurs se choisissent avec les flèches et Espace. Avec un thème à contraste élevé de Windows, Aldune utilise ses couleurs.",
        "• Tab passa de um botão a outro e um anel mostra onde você está; Espaço ou Enter aciona. As cores se escolhem com as setas e Espaço. Com um tema de alto contraste do Windows, o Aldune usa as cores dele.", "• Tab si sposta tra i pulsanti e un anello mostra dove sei; Spazio o Invio premono. I colori si scelgono con i tasti freccia e Spazio. Con un tema Windows ad alto contrasto, Aldune usa i suoi colori.", "• Tab でボタン間を移動すると、リングで現在位置が分かります。Space または Enter で押します。色は矢印キーと Space で選びます。Windows のハイコントラスト テーマでは、Aldune はそのテーマの色を使います。", "• Tab 在按钮之间移动，并用一个圆环显示您所在的位置；空格键或 Enter 按下。颜色用方向键和空格键选择。使用 Windows 高对比度主题时，Aldune 会采用该主题的颜色。");
    public static string QuickHelpDockGroup => T("The dock", "El dock",
        "Das Dock", "Le dock", "O dock", "Il dock", "ドック", "程序坞");
    public static string QuickHelpNotesGroup => T("Notes", "Notas",
        "Notizen", "Notes", "Notas", "Note", "ノート", "笔记");
    public static string QuickHelpTasksGroup => T("Tasks and lists", "Tareas y listas",
        "Aufgaben und Listen", "Tâches et listes", "Tarefas e listas", "Attività ed elenchi", "タスクとリスト", "任务和列表");
    public static string QuickHelpSyncGroup => T("Sync", "Sincronización",
        "Synchronisierung", "Synchronisation", "Sincronização", "Sincronizzazione", "同期", "同步");
    public static string QuickHelpTask => T("• Ctrl+L turns a line into a task. Click the checkbox (or press Ctrl+Enter) to check it off, and Enter keeps the list going on its own.", "• Ctrl+L convierte una línea en tarea. Haz clic en la casilla (o pulsa Ctrl+Enter) para marcarla, y Enter sigue la lista sola.",
        "• Ctrl+L verwandelt eine Zeile in eine Aufgabe. Klicke auf das Kästchen (oder drücke Ctrl+Enter), um sie abzuhaken, und Enter setzt die Liste selbst fort.",
        "• Ctrl+L transforme une ligne en tâche. Cliquez sur la case (ou appuyez sur Ctrl+Enter) pour la cocher, et Enter continue la liste tout seul.",
        "• Ctrl+L converte uma linha em tarefa. Clique na caixa (ou pressione Ctrl+Enter) para marcá-la, e Enter continua a lista sozinha.", "• Ctrl+L trasforma una riga in un'attività. Fai clic sulla casella (o premi Ctrl+Invio) per spuntarla, e Invio continua l'elenco da solo.", "• Ctrl+L で行をタスクに変えます。チェックボックスをクリック（または Ctrl+Enter）で完了にでき、Enter でリストが自動的に続きます。", "• Ctrl+L 把一行变成任务。点击复选框（或按 Ctrl+Enter）即可勾选，按 Enter 会自动延续列表。");
    public static string QuickHelpBullets => T("• Ctrl+Shift+L makes a bulleted list. Tab and Shift+Tab indent a line, for subtasks.", "• Ctrl+Shift+L hace una lista con viñetas. Tab y Mayús+Tab sangran la línea, para subtareas.",
        "• Ctrl+Shift+L erstellt eine Liste mit Aufzählungszeichen. Tab und Shift+Tab rücken eine Zeile ein, für Unteraufgaben.",
        "• Ctrl+Shift+L crée une liste à puces. Tab et Shift+Tab indentent une ligne, pour les sous-tâches.",
        "• Ctrl+Shift+L faz uma lista com marcadores. Tab e Shift+Tab recuam a linha, para subtarefas.", "• Ctrl+Maiusc+L crea un elenco puntato. Tab e Maiusc+Tab rientrano una riga, per le sottoattività.", "• Ctrl+Shift+L で箇条書きにします。Tab と Shift+Tab で行のインデントを変えると、サブタスクになります。", "• Ctrl+Shift+L 创建项目符号列表。Tab 和 Shift+Tab 缩进一行，用于子任务。");
    public static string QuickHelpLists => T("• For a list you reuse, like the shopping list: ⋯ > Uncheck all, or Remove checked tasks. In Settings, checked tasks can move to the end. Pasting a Markdown list (- [ ]) brings its checkboxes.", "• Para una lista que se repite, como la de la compra: ⋯ > Desmarcar todas, o Borrar las tareas hechas. En Ajustes, las hechas pueden bajar al final. Pegar una lista de Markdown (- [ ]) trae sus casillas.",
        "• Für eine Liste, die du immer wieder verwendest, wie die Einkaufsliste: ⋯ > Alle abhaken rückgängig machen oder Abgehakte Aufgaben entfernen. In den Einstellungen können abgehakte Aufgaben ans Ende verschoben werden. Das Einfügen einer Markdown-Liste (- [ ]) bringt ihre Kontrollkästchen mit.",
        "• Pour une liste réutilisable, comme la liste de courses : ⋯ > Décocher tout ou Supprimer les tâches cochées. Dans les paramètres, les tâches cochées peuvent passer en bas. Coller une liste Markdown (- [ ]) importe ses cases.",
        "• Para uma lista que você reutiliza, como a lista de compras: ⋯ > Desmarcar todas ou Remover tarefas concluídas. Em Configurações, as concluídas podem ir para o final. Colar uma lista Markdown (- [ ]) traz suas caixinhas.", "• Per un elenco che riutilizzi, come la lista della spesa: ⋯ > Deseleziona tutto, oppure Rimuovi le attività completate. Nelle Impostazioni, le attività completate possono andare in fondo. Incollando un elenco Markdown (- [ ]) arrivano anche le sue caselle.", "• 買い物リストのように繰り返し使うリストには、⋯ > すべてのチェックを外す、または 完了したタスクを削除 を使います。設定で、チェックしたタスクを末尾に移動させることもできます。Markdown のリスト (- [ ]) を貼り付けると、チェックボックスも反映されます。", "• 对于会重复使用的列表，比如购物清单：⋯ > 取消全部勾选，或删除已勾选的任务。在设置中，已勾选的任务可以移到末尾。粘贴 Markdown 列表 (- [ ]) 时，复选框也会一并带入。");
    public static string QuickHelpReminder => T("• ⋯ > Reminder notifies you at a set time, and the notice lists what's still to do.", "• ⋯ > Recordatorio te avisa a una hora, y el aviso dice qué tareas faltan.",
        "• ⋯ > Erinnerung benachrichtigt dich zu einer festgelegten Zeit, und die Benachrichtigung listet auf, was noch zu erledigen ist.",
        "• ⋯ > Rappel vous avertit à une heure définie, et la notification liste ce qu'il reste à faire.",
        "• ⋯ > Lembrete avisa você em um horário definido, e o aviso lista o que ainda falta fazer.", "• ⋯ > Promemoria ti avvisa a un'ora stabilita, e l'avviso elenca ciò che resta da fare.", "• ⋯ > リマインダーで、指定した時刻に通知されます。通知には残りの項目が一覧表示されます。", "• ⋯ > 提醒会在设定的时间通知您，通知中会列出尚未完成的内容。");
    public static string QuickHelpMoveLine => T("• Alt+Up/Down moves the current line up or down — handy for reordering a checklist.", "• Alt+Arriba/Abajo sube o baja la línea del cursor — útil para reordenar una lista de tareas.",
        "• Alt+Auf/Ab verschiebt die aktuelle Zeile nach oben oder unten — praktisch zum Umsortieren einer Checkliste.",
        "• Alt+Haut/Bas déplace la ligne actuelle vers le haut ou le bas — pratique pour réordonner une liste.",
        "• Alt+Cima/Baixo move a linha atual para cima ou para baixo — útil para reordenar uma lista de tarefas.", "• Alt+Su/Giù sposta la riga corrente in su o in giù — utile per riordinare una lista di controllo.", "• Alt+上/下で現在の行を上下に移動できます。チェックリストの並べ替えに便利です。", "• Alt+上/下 可上下移动当前行——整理清单时很方便。");
    public static string QuickHelpMenu => T("• The ⋯ button on a note: tasks, reminder, color, “always on top,” password, tags, export, archive and trash.", "• El botón ⋯ de una nota: tareas, recordatorio, color, «siempre encima», contraseña, etiquetas, exportar, archivar y papelera.",
        "• Die Schaltfläche ⋯ einer Notiz: Aufgaben, Erinnerung, Farbe, „Immer im Vordergrund“, Passwort, Tags, Exportieren, Archivieren und Papierkorb.",
        "• Le bouton ⋯ d'une note : tâches, rappel, couleur, « toujours au premier plan », mot de passe, étiquettes, exporter, archiver et corbeille.",
        "• O botão ⋯ de uma nota: tarefas, lembrete, cor, “sempre visível”, senha, etiquetas, exportar, arquivar e lixeira.", "• Il pulsante ⋯ di una nota: attività, promemoria, colore, “sempre in primo piano”, password, etichette, esportazione, archivio e cestino.", "• ノートの ⋯ ボタン: タスク、リマインダー、色、「常に手前に表示」、パスワード、タグ、エクスポート、アーカイブ、ごみ箱。", "• 笔记上的 ⋯ 按钮：任务、提醒、颜色、“始终置顶”、密码、标签、导出、归档和回收站。");
    public static string QuickHelpEscape => T("• Esc closes the open note without losing what you wrote (it autosaves).", "• Esc cierra la nota abierta sin perder lo escrito (se guarda solo).",
        "• Esc schließt die geöffnete Notiz, ohne das Geschriebene zu verlieren (wird automatisch gespeichert).",
        "• Esc ferme la note ouverte sans perdre ce que vous avez écrit (sauvegarde automatique).",
        "• Esc fecha a nota aberta sem perder o que foi escrito (salva automaticamente).", "• Esc chiude la nota aperta senza perdere ciò che hai scritto (salva da sola).", "• Esc で、書いた内容を失わずに開いているノートを閉じます（自動保存されます）。", "• Esc 关闭已打开的笔记，不会丢失您写的内容（会自动保存）。");
    public static string QuickHelpHotkeyOn(string combo) =>
        T($"• {combo} creates a new note from anywhere.", $"• {combo} crea una nota nueva desde cualquier sitio.",
            $"• {combo} erstellt eine neue Notiz von überall.",
            $"• {combo} crée une nouvelle note depuis n'importe où.",
            $"• {combo} cria uma nova nota de qualquer lugar.", $"• {combo} crea una nuova nota da qualsiasi punto.", $"• {combo} で、どこからでも新しいノートを作成できます。", $"• {combo} 可在任何地方创建新笔记。");
    public static string QuickHelpHotkeyOff => T("• The keyboard shortcut is off; turn it on above to create notes without going to the edge.", "• El atajo de teclado está desactivado; actívalo arriba para crear notas sin ir al borde.",
        "• Das Tastaturkürzel ist deaktiviert; aktiviere es oben, um Notizen zu erstellen, ohne zum Rand zu gehen.",
        "• Le raccourci clavier est désactivé ; activez-le ci-dessus pour créer des notes sans aller au bord.",
        "• O atalho de teclado está desativado; ative-o acima para criar notas sem ir até a borda.", "• La scorciatoia da tastiera è disattivata; attivala sopra per creare note senza andare al bordo.", "• キーボードショートカットはオフです。画面の端に行かずにノートを作成するには、上でオンにしてください。", "• 键盘快捷键已关闭；在上面开启它，即可无需移到屏幕边缘就创建笔记。");
    public static string QuickHelpTray => T("• The tray icon opens the notes manager and these settings.", "• El icono de la bandeja abre el gestor de notas y estos ajustes.",
        "• Das Taskleistensymbol öffnet den Notizenmanager und diese Einstellungen.",
        "• L'icône de la barre des tâches ouvre le gestionnaire de notes et ces paramètres.",
        "• O ícone da bandeja abre o gerenciador de notas e estas configurações.", "• L'icona nell'area di notifica apre il gestore delle note e queste impostazioni.", "• トレイのアイコンから、ノート管理とこの設定を開けます。", "• 托盘图标可打开笔记管理器和这些设置。");
    public static string QuickHelpHideDock => T(
        "• Ctrl+Alt+H hides the dock and brings it back — handy for fullscreen videos Windows doesn't report as such. The tray menu has the same switch.",
        "• Ctrl+Alt+H oculta el dock y lo devuelve — útil para vídeos a pantalla completa que Windows no reporta como tales. El menú de la bandeja tiene el mismo interruptor.",
        "• Ctrl+Alt+H blendet das Dock aus und wieder ein — praktisch für Vollbildvideos, die Windows nicht als solche erkennt. Das Taskleistenmenü hat denselben Schalter.",
        "• Ctrl+Alt+H masque le dock et le restaure — pratique pour les vidéos en plein écran que Windows ne signale pas comme telles. Le menu de la barre des tâches possède le même commutateur.",
        "• Ctrl+Alt+H oculta o dock e o exibe novamente — útil para vídeos em tela cheia que Windows não detecta como tal. O menu da bandeja tem o mesmo interruptor.", "• Ctrl+Alt+H nasconde il dock e lo riporta — utile per i video a schermo intero che Windows non segnala come tali. Il menu dell'icona ha lo stesso interruttore.", "• Ctrl+Alt+H でドックを隠したり、再び表示したりできます。Windows が全画面と認識しない動画に便利です。トレイのメニューにも同じスイッチがあります。", "• Ctrl+Alt+H 隐藏程序坞并可将其恢复——适合 Windows 未识别为全屏的视频。托盘菜单里有相同的开关。");
    public static string QuickHelpDockMenus => T(
        "• Right-click the dock buttons for views, tags, notes from the clipboard, settings and Keep dock open.",
        "• Clic derecho en los botones del dock para ver vistas, etiquetas, notas desde el portapapeles, Ajustes y Mantener el dock abierto.",
        "• Rechtsklick auf die Dock-Schaltflächen für Ansichten, Tags, Notizen aus der Zwischenablage, Einstellungen und Dock offen halten.",
        "• Clic droit sur les boutons du dock pour les vues, les étiquettes, les notes depuis le presse-papiers, les paramètres et Garder le dock ouvert.",
        "• Clique com o botão direito nos botões do dock para ver vistas, etiquetas, notas da área de transferência, Configurações e Manter o dock aberto.", "• Clic destro sui pulsanti del dock per viste, etichette, note dagli appunti, impostazioni e Mantieni il dock aperto.", "• ドックのボタンを右クリックすると、表示、タグ、クリップボードからのノート、設定、ドックを開いたままにする、が使えます。", "• 右键点击程序坞按钮可使用视图、标签、来自剪贴板的笔记、设置和保持程序坞展开。");
    public static string QuickHelpAutoHideTasks => T(
        "• Completed tasks can disappear automatically after the delay configured in Settings.",
        "• Las tareas completadas pueden desaparecer automáticamente tras el plazo configurado en Ajustes.",
        "• Abgehakte Aufgaben können nach der in den Einstellungen konfigurierten Verzögerung automatisch verschwinden.",
        "• Les tâches cochées peuvent disparaître automatiquement après le délai configuré dans les paramètres.",
        "• As tarefas concluídas podem desaparecer automaticamente após o prazo configurado em Configurações.", "• Le attività completate possono sparire automaticamente dopo il ritardo configurato nelle Impostazioni.", "• 完了したタスクは、設定で指定した時間が過ぎると自動的に消えるようにできます。", "• 已完成的任务可在设置中指定的延迟后自动消失。");
    public static string QuickHelpConflicts => T(
        "• Sync conflicts can be reviewed in Settings; restore the losing version or dismiss the record.",
        "• Los conflictos de sincronización se revisan en Ajustes; puedes restaurar la versión perdedora o descartar el registro.",
        "• Synchronisierungskonflikte können in den Einstellungen überprüft werden; stelle die unterlegene Version wieder her oder verwerfe den Eintrag.",
        "• Les conflits de synchronisation peuvent être examinés dans les paramètres ; restaurez la version perdante ou ignorez l'enregistrement.",
        "• Os conflitos de sincronização podem ser revisados em Configurações; restaure a versão perdedora ou descarte o registro.", "• I conflitti di sincronizzazione si possono rivedere nelle Impostazioni; ripristina la versione perdente oppure scarta il record.", "• 同期の競合は設定で確認できます。採用されなかったバージョンを復元するか、記録を破棄してください。", "• 同步冲突可在设置中查看；恢复落选的版本或忽略该记录。");
    public static string QuickHelpSync => T("• Sync now is available in the dock; automatic sync can be enabled in Settings.", "• Puedes sincronizar desde el dock y activar la sincronización automática en Ajustes.",
        "• Jetzt synchronisieren ist im Dock verfügbar; die automatische Synchronisierung kann in den Einstellungen aktiviert werden.",
        "• Synchroniser maintenant est disponible dans le dock ; la synchronisation automatique peut être activée dans les paramètres.",
        "• Sincronizar agora está disponível no dock; a sincronização automática pode ser ativada em Configurações.", "• Sincronizza ora è disponibile nel dock; la sincronizzazione automatica si può attivare nelle Impostazioni.", "• 「今すぐ同期」はドックにあります。自動同期は設定でオンにできます。", "• 程序坞中提供“立即同步”；自动同步可在设置中开启。");
    public static string QuickHelpSearch => T("• In Manage notes, Ctrl+F focuses the search box and selects the current search.", "• En Gestionar notas, Ctrl+F enfoca la búsqueda y selecciona el texto actual.",
        "• In Notizen verwalten setzt Ctrl+F den Fokus auf das Suchfeld und markiert die aktuelle Suche.",
        "• Dans Gérer les notes, Ctrl+F met le focus sur la zone de recherche et sélectionne la recherche actuelle.",
        "• Em Gerenciar notas, Ctrl+F foca a caixa de pesquisa e seleciona a busca atual.", "• In Gestisci le note, Ctrl+F porta il cursore sulla casella di ricerca e seleziona la ricerca corrente.", "• 「ノートを管理」では、Ctrl+F で検索ボックスにフォーカスし、現在の検索を選択します。", "• 在“管理笔记”中，Ctrl+F 会聚焦搜索框并选中当前搜索内容。");
    public static string QuickHelpAutoScroll => T(
        "• Middle-click on a long list or note turns on autoscroll: the view follows the cursor on its own; another click stops it.",
        "• El clic central sobre una lista larga o una nota activa el autodesplazamiento: la vista sigue al cursor sola; otro clic lo apaga.",
        "• Ein mittlerer Mausklick auf eine lange Liste oder Notiz aktiviert das automatische Scrollen: Die Ansicht folgt dem Cursor automatisch; ein weiterer Klick hält es an.",
        "• Un clic du milieu sur une longue liste ou une note active le défilement automatique : la vue suit le curseur toute seule ; un autre clic l'arrête.",
        "• O clique do meio em uma lista longa ou nota ativa a rolagem automática: a visualização segue o cursor sozinha; outro clique a desliga.", "• Il clic con il tasto centrale su un elenco o una nota lunga attiva lo scorrimento automatico: la vista segue il cursore da sola; un altro clic lo ferma.", "• 長いリストやノートでホイールをクリックすると、オートスクロールがオンになります。カーソルに合わせて表示が自動で動き、もう一度クリックすると止まります。", "• 在较长的列表或笔记上按下鼠标中键会开启自动滚动：视图会自行跟随光标移动；再次点击即可停止。");

    public static string MinimizeWindowTooltip => T("Minimize", "Minimizar",
        "Minimieren", "Réduire", "Minimizar", "Riduci a icona", "最小化", "最小化");
    public static string MaximizeWindowTooltip => T("Maximize", "Maximizar",
        "Maximieren", "Agrandir", "Maximizar", "Ingrandisci", "最大化", "最大化");
    public static string RestoreWindowTooltip => T("Restore", "Restaurar",
        "Wiederherstellen", "Restaurer", "Restaurar", "Ripristina", "元に戻す", "还原");

    // --- Arranque / errores de arranque --------------------------------------------------------------

    public static string UnexpectedErrorTitle => T("Aldune — error", "Aldune — error",
        "Aldune — Fehler", "Aldune — erreur", "Aldune — erro", "Aldune — errore", "Aldune — エラー", "Aldune — 错误");
    public static string UnexpectedErrorMessage(string details) =>
        T($"An unexpected error occurred: {details}\n\nThe application will continue, but this particular action may not have completed.",
          $"Ha ocurrido un error inesperado: {details}\n\nLa aplicación continuará, pero esta acción concreta puede no haberse completado.",
            $"Ein unerwarteter Fehler ist aufgetreten: {details}\\n\\nDie Anwendung wird fortgesetzt, aber diese bestimmte Aktion wurde möglicherweise nicht abgeschlossen.",
            $"Une erreur inattendue s'est produite : {details}\\n\\nL'application continuera, mais cette action en particulier peut ne pas s'être terminée.",
            $"Ocorreu um erro inesperado: {details}\\n\\nO aplicativo continuará, mas esta ação específica pode não ter sido concluída.", $"Si è verificato un errore imprevisto: {details}\n\nL'applicazione continuerà, ma questa azione potrebbe non essere stata completata.", $"予期しないエラーが発生しました: {details}\n\nアプリケーションは動作を続けますが、この操作は完了していない可能性があります。", $"发生了意外错误：{details}\n\n应用程序将继续运行，但这次操作可能没有完成。");

    public static string CannotStartTitle => T("Aldune — can't start", "Aldune — no se puede iniciar",
        "Aldune — Start nicht möglich", "Aldune — impossible de démarrer", "Aldune — não é possível iniciar", "Aldune — impossibile avviare", "Aldune — 起動できません", "Aldune — 无法启动");
    public static string DatabaseRecoveredTitle => T("Aldune — database recovered", "Aldune — base de datos recuperada",
        "Aldune — Datenbank wiederhergestellt",
        "Aldune — base de données restaurée",
        "Aldune — banco de dados recuperado", "Aldune — database recuperato", "Aldune — データベースを回復しました", "Aldune — 数据库已恢复");

    public static string KeyUnwrapFailedMessage => T(
        "The notes database can't be decrypted with the stored key.\n\n" +
        "The most likely cause is that this Windows user's password was reset, which permanently " +
        "destroys the protected key. The automatic backups are protected the same way, so they " +
        "can't recover it either.",
        "No se puede descifrar la base de datos de notas con la clave almacenada.\n\n" +
        "La causa más probable es que se haya restablecido la contraseña de Windows de este " +
        "usuario, lo que destruye de forma permanente la clave protegida. Las copias automáticas " +
        "están protegidas igual, así que tampoco pueden recuperarla.",
        "Die Notizdatenbank kann mit dem gespeicherten Schlüssel nicht entschlüsselt werden.\n\n" + "Die wahrscheinlichste Ursache ist, dass das Passwort dieses Windows-Benutzers zurückgesetzt wurde, was den geschützten Schlüssel dauerhaft zerstört. Die automatischen Sicherungen sind auf dieselbe Weise geschützt, sodass auch sie ihn nicht wiederherstellen können.",
        "La base de données de notes ne peut pas être déchiffrée avec la clé enregistrée.\n\n" + "La cause la plus probable est que le mot de passe de cet utilisateur Windows a été réinitialisé, ce qui détruit définitivement la clé protégée. Les sauvegardes automatiques sont protégées de la même façon, elles ne peuvent donc pas non plus la récupérer.",
        "O banco de dados de notas não pode ser descriptografado com a chave armazenada.\n\n" + "A causa mais provável é que a senha deste usuário do Windows foi redefinida, o que destrói permanentemente a chave protegida. Os backups automáticos são protegidos da mesma forma, portanto também não conseguem recuperá-la.", "Il database delle note non può essere decifrato con la chiave memorizzata.\n\nLa causa più probabile è che la password di questo utente di Windows sia stata reimpostata, cosa che distrugge in modo permanente la chiave protetta. Anche le copie di sicurezza automatiche sono protette allo stesso modo, quindi non possono recuperarla.", "保存されているキーではノートのデータベースを復号できません。\n\n最も考えられる原因は、この Windows ユーザーのパスワードがリセットされ、保護されたキーが完全に失われたことです。自動バックアップも同じ方法で保護されているため、そこからも回復できません。", "无法用已存储的密钥解密笔记数据库。\n\n最可能的原因是此 Windows 用户的密码被重置，从而永久销毁了受保护的密钥。自动备份也以同样的方式受到保护，因此同样无法恢复。");

    public static string MissingKeyMessage(string folder) => T(
        "The settings file that holds your notes' key is missing, and no backup has it.\n\n" +
        "Aldune won't create a new key, because your existing notes would become unreadable. If you " +
        $"have a copy of settings.json, put it in {folder} and open Aldune again.\n\n" +
        $"To start from scratch instead, move notes.db out of {folder}: Aldune will create a new, empty one.",
        "Falta el archivo de ajustes que guarda la clave de tus notas y ninguna copia la tiene.\n\n" +
        "Aldune no creará una clave nueva, porque tus notas dejarían de poder leerse. Si tienes una " +
        $"copia de settings.json, colócala en {folder} y vuelve a abrir Aldune.\n\n" +
        $"Si prefieres empezar de cero, saca notes.db de {folder}: Aldune creará uno nuevo y vacío.",
        "Die Einstellungsdatei, die den Schlüssel deiner Notizen enthält, fehlt, und keine Sicherung hat ihn.\\n\\n" + "Aldune erstellt keinen neuen Schlüssel, da deine vorhandenen Notizen sonst unlesbar würden. Wenn du eine " + $"Kopie von settings.json hast, lege sie in {folder} und öffne Aldune erneut.\\n\\n" + $"Um stattdessen neu zu beginnen, verschiebe notes.db aus {folder}: Aldune erstellt eine neue, leere Datei.",
        "Le fichier de paramètres contenant la clé de vos notes est manquant, et aucune sauvegarde ne le contient.\\n\\n" + "Aldune ne créera pas de nouvelle clé, car vos notes existantes deviendraient illisibles. Si vous " + $"avez une copie de settings.json, placez-la dans {folder} et rouvrez Aldune.\\n\\n" + $"Pour repartir de zéro, déplacez notes.db hors de {folder} : Aldune en créera un nouveau, vide.",
        "O arquivo de configurações que guarda a chave das suas notas está ausente e nenhum backup o possui.\\n\\n" + "Aldune não criará uma nova chave, pois suas notas existentes ficariam ilegíveis. Se você " + $"tiver uma cópia de settings.json, coloque-a em {folder} e abra o Aldune novamente.\\n\\n" + $"Para começar do zero, mova notes.db de {folder}: Aldune criará um novo, vazio.", "Il file di impostazioni che contiene la chiave delle tue note manca e nessuna copia di sicurezza lo ha.\n\nAldune non creerà una nuova chiave, perché le tue note esistenti diventerebbero illeggibili. Se hai una copia di settings.json, mettila in {folder} e riapri Aldune.\n\nPer ripartire da zero, sposta notes.db fuori da {folder}: Aldune ne creerà uno nuovo e vuoto.", "ノートのキーを保存している設定ファイルが見つからず、どのバックアップにもありません。\n\n既存のノートが読めなくなってしまうため、Aldune は新しいキーを作成しません。settings.json のコピーがある場合は、{folder} に置いて Aldune をもう一度開いてください。\n\n最初からやり直す場合は、notes.db を {folder} の外へ移動してください。Aldune が新しい空のデータベースを作成します。", "保存您笔记密钥的设置文件已丢失，而且没有任何备份包含它。\n\nAldune 不会创建新密钥，因为这会使您现有的笔记无法读取。如果您有 settings.json 的副本，请把它放到 {folder} 中，然后再次打开 Aldune。\n\n如果想从头开始，请把 notes.db 移出 {folder}：Aldune 会创建一个全新的空数据库。");

    public static string KeyRestoredFromBackupMessage(string day) => T(
        $"Your notes' key was missing from the settings and has been restored from the backup of {day}.",
        $"La clave de tus notas faltaba en los ajustes y se ha recuperado de la copia del {day}.",
        $"Der Schlüssel deiner Notizen fehlte in den Einstellungen und wurde aus der Sicherung vom {day} wiederhergestellt.",
        $"La clé de vos notes était absente des paramètres et a été restaurée à partir de la sauvegarde du {day}.",
        $"A chave das suas notas estava ausente nas configurações e foi recuperada do backup de {day}.", $"La chiave delle tue note mancava dalle impostazioni ed è stata ripristinata dalla copia di sicurezza del {day}.", $"ノートのキーが設定から失われていたため、{day} のバックアップから復元しました。", $"您笔记的密钥已从设置中丢失，现已从 {day} 的备份中恢复。");

    public static string SettingsRestoredFromBackupMessage(string day) => T(
        $"The settings file was damaged and the backup of {day} has been restored. Check any setting you changed after that day; if sync fails, import your sync code again.",
        $"El archivo de ajustes estaba dañado y se ha restaurado la copia del {day}. Revisa los ajustes que cambiaras después de ese día; si la sincronización falla, vuelve a importar tu código.",
        $"Die Einstellungsdatei war beschädigt und die Sicherung vom {day} wurde wiederhergestellt. Überprüfe alle Einstellungen, die du nach diesem Tag geändert hast; falls die Synchronisierung fehlschlägt, importiere deinen Code erneut.",
        $"Le fichier de paramètres était endommagé et la sauvegarde du {day} a été restaurée. Vérifiez les paramètres modifiés après ce jour ; si la synchronisation échoue, réimportez votre code.",
        $"O arquivo de configurações estava danificado e o backup de {day} foi restaurado. Verifique qualquer configuração que você tenha alterado após esse dia; se a sincronização falhar, importe seu código novamente.", $"Il file di impostazioni era danneggiato ed è stata ripristinata la copia di sicurezza del {day}. Controlla le impostazioni modificate dopo quel giorno; se la sincronizzazione non funziona, importa di nuovo il codice di sincronizzazione.", $"設定ファイルが破損していたため、{day} のバックアップを復元しました。その日以降に変更した設定を確認してください。同期に失敗する場合は、同期コードをもう一度インポートしてください。", $"设置文件已损坏，已恢复 {day} 的备份。请检查那天之后您更改过的设置；如果同步失败，请重新导入同步码。");

    public static string DataRecoveredTitle => T("Aldune — data recovered", "Aldune — datos recuperados",
        "Aldune — Daten wiederhergestellt", "Aldune — données restaurées", "Aldune — dados recuperados", "Aldune — dati recuperati", "Aldune — データを回復しました", "Aldune — 数据已恢复");

    public static string DatabaseUnrecoverableMessage => T(
        "The notes database couldn't be opened or recreated after an automatic recovery attempt. " +
        "The disk may be full, or the file may still be damaged.",
        "No se ha podido abrir ni recrear la base de datos de notas tras un intento de " +
        "recuperación automática. Es posible que el disco esté lleno o que el archivo siga " +
        "dañado.",
        "Die Notizdatenbank konnte nach einem automatischen Wiederherstellungsversuch nicht geöffnet oder neu erstellt werden. " + "Eventuell ist die Festplatte voll oder die Datei ist noch beschädigt.",
        "La base de données de notes n'a pas pu être ouverte ni recréée après une tentative de restauration automatique. " + "Le disque est peut-être plein ou le fichier est encore endommagé.",
        "O banco de dados de notas não pôde ser aberto nem recriado após uma tentativa de recuperação automática. " + "O disco pode estar cheio ou o arquivo ainda pode estar danificado.", "Non è stato possibile aprire né ricreare il database delle note dopo un tentativo di recupero automatico. Il disco potrebbe essere pieno, oppure il file potrebbe essere ancora danneggiato.", "自動回復を試みた後も、ノートのデータベースを開けず、再作成もできませんでした。ディスクがいっぱいか、ファイルがまだ破損している可能性があります。", "在尝试自动恢复之后，仍无法打开或重新创建笔记数据库。可能是磁盘已满，或文件仍然损坏。");

    public static string UnexpectedStartupFailureMessage(string details) =>
        T($"Aldune couldn't start due to an unexpected error: {details}",
          $"No se ha podido iniciar Aldune debido a un error inesperado: {details}",
            $"Aldune konnte aufgrund eines unerwarteten Fehlers nicht gestartet werden: {details}",
            $"Aldune n'a pas pu démarrer en raison d'une erreur inattendue : {details}",
            $"Não foi possível iniciar o Aldune devido a um erro inesperado: {details}", $"Aldune non è riuscito ad avviarsi a causa di un errore imprevisto: {details}", $"予期しないエラーのため、Aldune を起動できませんでした: {details}", $"Aldune 因意外错误无法启动：{details}");

    public static string NoMonitorsMessage => T("No connected monitor could be detected.", "No se ha podido detectar ningún monitor conectado.",
        "Es konnte kein angeschlossener Monitor erkannt werden.",
        "Aucun moniteur connecté n'a pu être détecté.",
        "Nenhum monitor conectado pôde ser detectado.", "Non è stato rilevato alcun monitor collegato.", "接続されているモニターを検出できませんでした。", "未检测到已连接的显示器。");

    public static string KeyMismatchMessage => T(
        "The notes database couldn't be decrypted with the current key.\n\n" +
        "The most likely cause is that the configuration file holding the key was lost, replaced, " +
        "or comes from another install. Your notes have NOT been deleted and remain stored " +
        "securely, but can't be read right now.",
        "La base de datos de notas no se ha podido descifrar con la clave actual.\n\n" +
        "La causa más probable es que el archivo de configuración que contenía la clave se " +
        "haya perdido, sustituido o proceda de otra instalación. Las notas NO se han eliminado " +
        "y siguen almacenadas de forma segura, pero no se pueden leer en este momento.",
        "Die Notizdatenbank konnte mit dem aktuellen Schlüssel nicht entschlüsselt werden.\n\n" + "Die wahrscheinlichste Ursache ist, dass die Konfigurationsdatei mit dem Schlüssel verloren gegangen, ersetzt wurde oder von einer anderen Installation stammt. Deine Notizen wurden NICHT gelöscht und werden weiterhin sicher gespeichert, können aber gerade nicht gelesen werden.",
        "La base de données de notes n'a pas pu être déchiffrée avec la clé actuelle.\n\n" + "La cause la plus probable est que le fichier de configuration contenant la clé a été perdu, remplacé ou provient d'une autre installation. Vos notes N'ONT PAS été supprimées et restent stockées en sécurité, mais ne peuvent pas être lues pour le moment.",
        "O banco de dados de notas não pôde ser descriptografado com a chave atual.\n\n" + "A causa mais provável é que o arquivo de configuração que continha a chave foi perdido, substituído ou vem de outra instalação. Suas notas NÃO foram excluídas e permanecem armazenadas com segurança, mas não podem ser lidas agora.", "Il database delle note non può essere decifrato con la chiave attuale.\n\nLa causa più probabile è che il file di configurazione che contiene la chiave sia stato perso, sostituito o provenga da un'altra installazione. Le tue note NON sono state eliminate e restano archiviate in modo sicuro, ma al momento non si possono leggere.", "現在のキーではノートのデータベースを復号できません。\n\n最も考えられる原因は、キーを保存している構成ファイルが失われた、置き換えられた、または別のインストールのものであることです。ノートは削除されておらず、安全に保存されたままですが、現時点では読み取れません。", "无法用当前密钥解密笔记数据库。\n\n最可能的原因是保存密钥的配置文件已丢失、被替换，或来自另一次安装。您的笔记没有被删除，仍被安全存放，但目前无法读取。");

    public static string DatabaseRecoveredMessage => T(
        "The existing notes file was damaged. A copy was kept in a \".corrupt-<date>\" file next " +
        "to the original, and a fresh, empty database was created.",
        "El archivo de notas existente estaba dañado. Se ha conservado una copia en un archivo " +
        "\".corrupt-<fecha>\" junto al original, y se ha creado una base de datos nueva y vacía.",
        "Die vorhandene Notizdatei war beschädigt. Eine Kopie wurde in einer Datei \".corrupt-<Datum>\" neben dem Original aufbewahrt, und eine neue, leere Datenbank wurde erstellt.",
        "Le fichier de notes existant était endommagé. Une copie a été conservée dans un fichier \".corrupt-<date>\" à côté de l'original, et une nouvelle base de données vide a été créée.",
        "O arquivo de notas existente estava danificado. Uma cópia foi mantida em um arquivo \".corrupt-<data>\" ao lado do original, e um novo banco de dados vazio foi criado.", "Il file delle note esistente era danneggiato. Una copia è stata conservata in un file \".corrupt-<data>\" accanto all'originale ed è stato creato un database nuovo e vuoto.", "既存のノートのファイルが破損していました。コピーは元のファイルの横に「.corrupt-<日付>」ファイルとして残し、新しい空のデータベースを作成しました。", "现有的笔记文件已损坏。副本已保存在原文件旁的“.corrupt-<日期>”文件中，并已创建一个全新的空数据库。");

    // --- Notas vinculadas a archivos ---
    public static string LinkedOpenFile => T("Open file…", "Abrir archivo…", "Datei öffnen…", "Ouvrir un fichier…", "Abrir arquivo…", "Apri file…", "ファイルを開く…", "打开文件…");
    public static string LinkedFileFilter => T("Text and Markdown files", "Archivos de texto y Markdown",
        "Text- und Markdown-Dateien", "Fichiers texte et Markdown", "Arquivos de texto e Markdown", "File di testo e Markdown", "テキストおよび Markdown ファイル", "文本和 Markdown 文件")
        + " (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt";
    public static string LinkedUnsupported(string name) => T(
        $"{name} isn't a .md, .markdown or .txt file.", $"{name} no es un archivo .md, .markdown ni .txt.",
        $"{name} ist keine .md-, .markdown- oder .txt-Datei.", $"{name} n'est pas un fichier .md, .markdown ou .txt.",
        $"{name} não é um arquivo .md, .markdown nem .txt.", $"{name} non è un file .md, .markdown o .txt.", $"{name} は .md、.markdown、.txt のいずれのファイルでもありません。", $"{name} 不是 .md、.markdown 或 .txt 文件。");
    public static string LinkedTooLarge(string name) => T(
        $"{name} is larger than 2 MB and can't be opened as a note.", $"{name} ocupa más de 2 MB y no se puede abrir como nota.",
        $"{name} ist größer als 2 MB und kann nicht als Notiz geöffnet werden.", $"{name} dépasse 2 Mo et ne peut pas être ouvert comme note.",
        $"{name} tem mais de 2 MB e não pode ser aberto como nota.", $"{name} supera i 2 MB e non può essere aperto come nota.", $"{name} は 2 MB を超えているため、ノートとして開けません。", $"{name} 超过 2 MB，无法作为笔记打开。");
    public static string LinkedBadEncoding(string name) => T(
        $"{name} isn't UTF-8 or UTF-16 text. Aldune won't open it so as not to damage its accents.",
        $"{name} no está en UTF-8 ni en UTF-16. Aldune no lo abre para no estropear sus tildes.",
        $"{name} ist kein UTF-8- oder UTF-16-Text. Aldune öffnet die Datei nicht, um Sonderzeichen nicht zu beschädigen.",
        $"{name} n'est pas en UTF-8 ni en UTF-16. Aldune ne l'ouvre pas pour ne pas abîmer ses accents.",
        $"{name} não está em UTF-8 nem em UTF-16. O Aldune não o abre para não estragar os acentos.", $"{name} non è testo UTF-8 o UTF-16. Aldune non lo aprirà per non rovinarne gli accenti.", $"{name} は UTF-8 または UTF-16 のテキストではありません。文字が壊れないよう、Aldune は開きません。", $"{name} 不是 UTF-8 或 UTF-16 文本。为避免损坏其中的字符，Aldune 不会打开它。");
    public static string LinkedUnreadable(string name) => T(
        $"{name} couldn't be read.", $"No se pudo leer {name}.", $"{name} konnte nicht gelesen werden.",
        $"Impossible de lire {name}.", $"Não foi possível ler {name}.", $"Impossibile leggere {name}.", $"{name} を読み込めませんでした。", $"无法读取 {name}。");
    public static string LinkedAlreadyLinkedElsewhere(string name) => T(
        $"{name} is already open in another note.", $"{name} ya está abierto en otra nota.",
        $"{name} ist bereits in einer anderen Notiz geöffnet.", $"{name} est déjà ouvert dans une autre note.",
        $"{name} já está aberto em outra nota.", $"{name} è già aperto in un'altra nota.", $"{name} はすでに別のノートで開かれています。", $"{name} 已在另一条笔记中打开。");
    public static string LinkedUnavailable(string path) => T(
        $"File not available: {path}", $"Archivo no disponible: {path}", $"Datei nicht verfügbar: {path}",
        $"Fichier indisponible : {path}", $"Arquivo indisponível: {path}", $"File non disponibile: {path}", $"ファイルを利用できません: {path}", $"文件不可用：{path}");
    public static string LinkedRetry => T("Retry", "Reintentar", "Erneut versuchen", "Réessayer", "Tentar de novo", "Riprova", "再試行", "重试");
    public static string LinkedLocate => T("Find…", "Buscar…", "Suchen…", "Rechercher…", "Procurar…", "Trova…", "検索…", "查找…");
    public static string LinkedConvert => T("Convert to normal note", "Convertir en nota normal",
        "In normale Notiz umwandeln", "Convertir en note normale", "Converter em nota normal", "Converti in nota normale", "通常のノートに変換", "转换为普通笔记");
    public static string LinkedConvertWarning(string path) => T(
        $"The note keeps its text and stops following the file. The file stays on disk, as plain text:\n{path}",
        $"La nota conserva su texto y deja de seguir al archivo. El archivo sigue en el disco, en claro:\n{path}",
        $"Die Notiz behält ihren Text und folgt der Datei nicht mehr. Die Datei bleibt unverschlüsselt auf dem Datenträger:\n{path}",
        $"La note garde son texte et ne suit plus le fichier. Le fichier reste sur le disque, en clair :\n{path}",
        $"A nota mantém o texto e deixa de acompanhar o arquivo. O arquivo continua no disco, sem criptografia:\n{path}", $"La nota mantiene il suo testo e smette di seguire il file. Il file resta sul disco, come testo semplice:\n{path}", $"ノートはテキストを保ったまま、ファイルとの連動を終了します。ファイルはプレーンテキストとしてディスクに残ります:\n{path}", $"笔记会保留其文本，并不再跟随该文件。文件仍以纯文本形式保留在磁盘上：\n{path}");
    public static string LinkedConflictPrefix => T("⚠ Conflict: ", "⚠ Conflicto: ", "⚠ Konflikt: ", "⚠ Conflit : ", "⚠ Conflito: ", "⚠ Conflitto:", "⚠ 競合:", "⚠ 冲突：");
    public static string LinkedConflictToast(string title) => T(
        $"{title} changed in another program while you were editing it here. Your version is in a new note.",
        $"{title} cambió en otro programa mientras lo editabas aquí. Tu versión está en una nota nueva.",
        $"{title} wurde in einem anderen Programm geändert, während du es hier bearbeitet hast. Deine Version steht in einer neuen Notiz.",
        $"{title} a été modifié dans un autre programme pendant que vous l'éditiez ici. Votre version est dans une nouvelle note.",
        $"{title} mudou em outro programa enquanto você o editava aqui. Sua versão está em uma nota nova.", $"{title} è cambiato in un altro programma mentre lo modificavi qui. La tua versione è in una nuova nota.", $"{title} が、ここで編集している間に別のプログラムで変更されました。あなたのバージョンは新しいノートに保存されています。", $"在您于此处编辑期间，{title} 在另一个程序中被修改。您的版本保存在一条新笔记中。");
    public static string LinkedSyncThisNote => T("Sync this note", "Sincronizar esta nota",
        "Diese Notiz synchronisieren", "Synchroniser cette note", "Sincronizar esta nota", "Sincronizza questa nota", "このノートを同期", "同步此笔记");
    public static string LinkedOpenInEditor => T("Open in its editor", "Abrir en su editor",
        "Im zugehörigen Editor öffnen", "Ouvrir dans son éditeur", "Abrir no editor padrão", "Apri nel suo editor", "エディターで開く", "在其编辑器中打开");
    public static string LinkedShowInExplorer => T("Show in File Explorer", "Mostrar en el Explorador",
        "Im Explorer anzeigen", "Afficher dans l'Explorateur", "Mostrar no Explorador", "Mostra in Esplora file", "エクスプローラーで表示", "在文件资源管理器中显示");
    public static string LinkedCopyPath => T("Copy file path", "Copiar la ruta del archivo",
        "Dateipfad kopieren", "Copier le chemin du fichier", "Copiar o caminho do arquivo", "Copia il percorso del file", "ファイルのパスをコピー", "复制文件路径");
    public static string ImportMarkdown => T("Import Markdown as a note…", "Importar Markdown como nota…",
        "Markdown als Notiz importieren…", "Importer du Markdown comme note…", "Importar Markdown como nota…", "Importa Markdown come nota…", "Markdown をノートとしてインポート…", "将 Markdown 导入为笔记…");
    public static string LinkedSaveAs => T("Save as linked file…", "Guardar como archivo vinculado…",
        "Als verknüpfte Datei speichern…", "Enregistrer comme fichier lié…", "Salvar como arquivo vinculado…", "Salva come file collegato…", "リンクされたファイルとして保存…", "另存为链接文件…");
    public static string LinkedSaveAsExists(string name) => T(
        $"{name} already exists. Aldune doesn't replace files: choose another name.",
        $"{name} ya existe. Aldune no reemplaza archivos: elige otro nombre.",
        $"{name} existiert bereits. Aldune ersetzt keine Dateien: Wähle einen anderen Namen.",
        $"{name} existe déjà. Aldune ne remplace pas de fichiers : choisissez un autre nom.",
        $"{name} já existe. O Aldune não substitui arquivos: escolha outro nome.", $"{name} esiste già. Aldune non sostituisce i file: scegli un altro nome.", $"{name} はすでに存在します。Aldune はファイルを置き換えません。別の名前を選んでください。", $"{name} 已存在。Aldune 不会替换文件：请选择另一个名称。");
    public static string LinkedProtectDisabled => T(
        "A linked note is the file itself, and the file is plain text. Convert it to a normal note to protect it.",
        "Una nota vinculada es el propio archivo, y el archivo está en claro. Conviértela en nota normal para protegerla.",
        "Eine verknüpfte Notiz ist die Datei selbst, und die Datei ist unverschlüsselt. Wandle sie in eine normale Notiz um, um sie zu schützen.",
        "Une note liée est le fichier lui-même, et le fichier est en clair. Convertissez-la en note normale pour la protéger.",
        "Uma nota vinculada é o próprio arquivo, e o arquivo está sem criptografia. Converta-a em nota normal para protegê-la.", "Una nota collegata è il file stesso, e il file è testo semplice. Convertila in una nota normale per proteggerla.", "リンクされたノートはファイルそのものであり、ファイルはプレーンテキストです。保護するには、通常のノートに変換してください。", "链接笔记就是文件本身，而文件是纯文本。要保护它，请先将其转换为普通笔记。");
}
