using Aldune.Core;

namespace Aldune.Resources;

/// <summary>
/// Todos los textos de la interfaz, en todos los idiomas, uno al lado del otro: inglés, español,
/// alemán, francés y portugués de Brasil (ver <see cref="UiLanguages"/>).
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

    // Orden fijo: inglés, español, alemán, francés, portugués (de Brasil). Una traducción que falte
    // cae al inglés, nunca a un texto vacío.
    private static string T(string en, string es, string? de = null, string? fr = null, string? pt = null) => Current switch
    {
        "es" => es,
        "de" => de ?? en,
        "fr" => fr ?? en,
        "pt" => pt ?? en,
        _ => en
    };

    // --- General / compartido entre ventanas -----------------------------------------------------

    public static string AppName => BrandIdentity.AppName;
    public static string ReminderToastTitle => T("Reminder", "Recordatorio",
        "Erinnerung", "Rappel", "Lembrete");
    public static string ReminderOpenNote => T("Open note", "Abrir nota",
        "Notiz öffnen", "Ouvrir la note", "Abrir nota");
    public static string ReminderShowNotes => T("Show notes", "Ver notas",
        "Notizen anzeigen", "Afficher les notes", "Ver notas");
    public static string WelcomeOpenManager => T("Show my notes", "Ver mis notas",
        "Meine Notizen anzeigen", "Afficher mes notes", "Ver minhas notas");
    public static string ReminderPendingTasks(int pending, IReadOnlyList<string> first)
    {
        var list = string.Join(", ", first) + (pending > first.Count ? "…" : "");
        return T($"Still to do ({pending}): {list}", $"Falta ({pending}): {list}",
            $"Noch zu erledigen ({pending}): {list}",
            $"Reste à faire ({pending}) : {list}",
            $"Ainda por fazer ({pending}): {list}");
    }
    public static string ReminderManyDue(int count) => T($"{count} reminders pending", $"{count} recordatorios pendientes",
        $"{count} Erinnerungen ausstehend", $"{count} rappels en attente", $"{count} lembretes pendentes");
    public static string Cut => T("Cut", "Cortar",
        "Ausschneiden", "Couper", "Recortar");
    public static string Copy => T("Copy", "Copiar",
        "Kopieren", "Copier", "Copiar");
    public static string Paste => T("Paste", "Pegar",
        "Einfügen", "Coller", "Colar");
    public static string Archive => T("Archive", "Archivar",
        "Archivieren", "Archiver", "Arquivar");
    public static string Restore => T("Restore", "Restaurar",
        "Wiederherstellen", "Restaurer", "Restaurar");
    public static string MoveToTrash => T("Move to trash", "Mover a la papelera",
        "In den Papierkorb verschieben", "Déplacer vers la corbeille", "Mover para a lixeira");
    public static string Trash => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira");
    public static string Archived => T("Archived", "Archivada",
        "Archiviert", "Archivée", "Arquivada");

    /// <summary>
    /// El título que muestran la pestaña del dock y la barra de tareas para una nota sin texto
    /// todavía. Se copia a <see cref="Aldune.Core.NoteTitleHelper.PlaceholderTitle"/> al arrancar
    /// (Core no depende de idiomas) — ver <c>App.OnStartup</c>.
    /// </summary>
    public static string NewNotePlaceholder => T("Untitled", "Sin título",
        "Ohne Titel", "Sans titre", "Sem título");

    // --- Ventana de nota --------------------------------------------------------------------------

    public static string TitlePlaceholder => T("Title", "Título",
        "Titel", "Titre", "Título");
    public static string BodyPlaceholder => T("Write something…  ·  Ctrl+L for a task", "Escribe algo…  ·  Ctrl+L para una tarea",
        "Schreib etwas…  ·  Ctrl+L für eine Aufgabe",
        "Écrivez quelque chose…  ·  Ctrl+L pour une tâche",
        "Escreva algo…  ·  Ctrl+L para uma tarefa");
    public static string MoreActionsTooltip => T("More actions", "Más acciones",
        "Weitere Aktionen", "Plus d'actions", "Mais ações");
    public static string CloseTooltip => T("Close (Esc)", "Cerrar (Esc)",
        "Schließen (Esc)", "Fermer (Esc)", "Fechar (Esc)");
    public static string ConvertToTask => T("Convert to task", "Convertir en tarea",
        "In Aufgabe umwandeln", "Convertir en tâche", "Converter em tarefa");
    public static string ConvertToBullet => T("Convert to list", "Convertir en lista",
        "In Liste umwandeln", "Convertir en liste", "Converter em liste");
    public static string UncheckAllTasks => T("Uncheck all", "Desmarcar todas",
        "Alle abwählen", "Tout décocher", "Desmarcar todas");
    public static string RemoveCheckedTasks => T("Remove checked tasks", "Borrar las tareas hechas",
        "Erledigte Aufgaben entfernen", "Supprimer les tâches terminées", "Remover tarefas concluídas");
    public static string AllTasksDone => T("✓ All done", "✓ Todo hecho",
        "✓ Alles erledigt", "✓ Tout est fait", "✓ Tudo pronto");
    public static string CustomColor => T("Choose another color…", "Elegir otro color…",
        "Andere Farbe wählen…", "Choisir une autre couleur…", "Escolher outra cor…");
    public static string CustomColorContrastError => T(
        "Enter a valid HEX color.",
        "Escribe un color HEX válido.",
        "Gib eine gültige HEX-Farbe ein.", "Entrez une couleur HEX valide.", "Digite uma cor HEX válida.");
    public static string CustomColorWindowTitle => T("Custom note color", "Color personalizado de la nota",
        "Benutzerdefinierte Notizfarbe", "Couleur de note personnalisée", "Cor personalizada da nota");
    public static string CustomColorWindowHint => T(
        "Text color adapts to keep your note readable.",
        "El color del texto se adapta para que la nota siga siendo legible.",
        "Die Textfarbe passt sich an, damit deine Notiz lesbar bleibt.",
        "La couleur du texte s'adapte pour que votre note reste lisible.",
        "A cor do texto se adapta para manter sua nota legível.");
    public static string CustomColorPreview => T("Preview", "Vista previa",
        "Vorschau", "Aperçu", "Visualização");
    public static string CustomColorSpectrumHint => T(
        "Pick a tone visually, then fine-tune it below.",
        "Elige un tono visualmente y ajústalo debajo.",
        "Wähle visuell einen Farbton und passe ihn unten an.",
        "Choisissez une nuance visuellement, puis ajustez-la ci-dessous.",
        "Escolha um tom visualmente e ajuste-o abaixo.");
    public static string CustomColorHexLabel => T("HEX color", "Color HEX",
        "HEX-Farbe", "Couleur HEX", "Cor HEX");
    public static string CustomColorRgbLabel => T("Fine tune with RGB", "Ajuste fino con RGB",
        "Feinabstimmung mit RGB", "Affiner avec RVB", "Ajuste fino com RGB");
    public static string CustomColorReadable => T("Readable text", "Texto legible",
        "Lesbarer Text", "Texte lisible", "Texto legível");
    public static string CustomColorInvalidHex => T("Enter a color like #F5E3B3.", "Escribe un color como #F5E3B3.",
        "Gib eine Farbe wie #F5E3B3 ein.",
        "Entrez une couleur comme #F5E3B3.",
        "Digite uma cor como #F5E3B3.");
    public static string DialogOk => T("OK", "Aceptar",
        "OK", "OK", "OK");
    public static string DialogCancel => T("Cancel", "Cancelar",
        "Abbrechen", "Annuler", "Cancelar");
    public static string DialogYes => T("Yes", "Sí",
        "Ja", "Oui", "Sim");
    public static string DialogNo => T("No", "No",
        "Nein", "Non", "Não");
    public static string CustomColorCancel => T("Cancel", "Cancelar",
        "Abbrechen", "Annuler", "Cancelar");
    public static string CustomColorApply => T("Use this color", "Usar este color",
        "Diese Farbe verwenden", "Utiliser cette couleur", "Usar esta cor");
    public static string RestoreSize => T("Restore size", "Restaurar tamaño",
        "Größe wiederherstellen", "Restaurer la taille", "Restaurar tamanho");
    public static string ExportToMarkdown => T("Export to Markdown", "Exportar a Markdown",
        "Nach Markdown exportieren", "Exporter vers Markdown", "Exportar para Markdown");
    public static string MarkdownFileFilter => T("Markdown file (*.md)|*.md", "Archivo Markdown (*.md)|*.md",
        "Markdown-Datei (*.md)|*.md", "Fichier Markdown (*.md)|*.md", "Arquivo Markdown (*.md)|*.md");

    // --- Actualizaciones ---------------------------------------------------------------------------

    public static string TrayCheckForUpdates => T("Check for updates…", "Buscar actualizaciones…",
        "Nach Updates suchen…", "Rechercher des mises à jour…", "Verificar atualizações…");
    public static string CheckForUpdatesAutomaticallyCheckbox => T("Check for new versions automatically", "Buscar versiones nuevas automáticamente",
        "Automatisch nach neuen Versionen suchen",
        "Rechercher automatiquement les nouvelles versions",
        "Verificar novas versões automaticamente");
    public static string CheckForUpdatesAutomaticallyHint => T("Once a day Aldune asks GitHub for the list of releases. Nothing about you or your notes is sent. Turned off, it only checks when you press the button.", "Una vez al día Aldune pide a GitHub la lista de versiones publicadas. No se envía nada tuyo ni de tus notas. Si lo desactivas, solo se comprueba al pulsar el botón.",
        "Einmal täglich fragt Aldune bei GitHub nach der Liste veröffentlichter Versionen. Es werden keine Daten über dich oder deine Notizen gesendet. Wenn deaktiviert, wird nur geprüft, wenn du auf die Schaltfläche klickst.",
        "Une fois par jour, Aldune demande à GitHub la liste des versions publiées. Rien sur vous ou vos notes n'est envoyé. Si désactivé, la vérification ne se fait que lorsque vous appuyez sur le bouton.",
        "Uma vez por dia o Aldune consulta o GitHub para obter a lista de versões. Nada sobre você ou suas notas é enviado. Quando desativado, só verifica ao clicar no botão.");
    // --- Exportar e importar la configuración (Ajustes, Acerca de) -----------------------------------

    public static string ConfigSectionTitle => T("Configuration", "Configuración",
        "Konfiguration", "Configuration", "Configuração");
    public static string ConfigAboutHint => T(
        "Save your appearance and settings to a file, or load them from one: to use them on another computer or to come back to them later.",
        "Guarda tu aspecto y tus ajustes en un archivo, o cárgalos desde uno: para usarlos en otro equipo o volver a ellos más adelante.",
        "Speichere Erscheinungsbild und Einstellungen in einer Datei oder lade sie aus einer Datei: für einen anderen Computer oder um später darauf zurückzukommen.",
        "Enregistrez votre apparence et vos paramètres dans un fichier, ou chargez-les depuis un fichier : pour les utiliser sur un autre ordinateur ou y revenir plus tard.",
        "Salve sua aparência e suas configurações em um arquivo, ou carregue-as de um arquivo: para usá-las em outro computador ou voltar a elas mais tarde.");
    public static string ConfigExportButton => T("Export settings…", "Exportar configuración…",
        "Einstellungen exportieren…", "Exporter la configuration…", "Exportar configuração…");
    public static string ConfigImportButton => T("Import settings…", "Importar configuración…",
        "Einstellungen importieren…", "Importer la configuration…", "Importar configuração…");
    public static string ConfigExportTitle => T("Export settings", "Exportar configuración",
        "Einstellungen exportieren", "Exporter la configuration", "Exportar configuração");
    public static string ConfigExportHint => T(
        "Saves your choices to a file you can import on another computer. Sync settings, keys and passwords, the chosen screen and note positions are never included.",
        "Guarda tus preferencias en un archivo que puedes importar en otro equipo. La sincronización, las claves y contraseñas, la pantalla elegida y las posiciones de las notas nunca se incluyen.",
        "Speichert deine Einstellungen in einer Datei, die du auf einem anderen Computer importieren kannst. Synchronisierung, Schlüssel und Passwörter, der gewählte Bildschirm und die Notizpositionen werden nie exportiert.",
        "Enregistre vos préférences dans un fichier que vous pouvez importer sur un autre ordinateur. La synchronisation, les clés et mots de passe, l'écran choisi et la position des notes ne sont jamais inclus.",
        "Salva suas preferências em um arquivo que você pode importar em outro computador. A sincronização, as chaves e senhas, a tela escolhida e as posições das notas nunca são incluídas.");
    public static string ConfigSectionAppearance => T("Appearance", "Aspecto",
        "Erscheinungsbild", "Apparence", "Aparência");
    public static string ConfigSectionAppearanceHint => T(
        "Look, colors, corners, sync signal, note theme and your own themes.",
        "Aspecto, colores, esquinas, señal de sincronización, tema de notas y temas propios.",
        "Aussehen, Farben, Ecken, Sync-Signal, Notizthema und eigene Themen.",
        "Apparence, couleurs, coins, signal de synchronisation, thème des notes et thèmes personnalisés.",
        "Aparência, cores, cantos, sinal de sincronização, tema das notas e temas próprios.");
    public static string ConfigSectionSettings => T("Settings", "Ajustes",
        "Einstellungen", "Paramètres", "Configurações");
    public static string ConfigSectionSettingsHint => T(
        "Language, shortcuts, dock, tasks, trash and more.",
        "Idioma, atajos, dock, tareas, papelera y más.",
        "Sprache, Tastenkürzel, Dock, Aufgaben, Papierkorb und mehr.",
        "Langue, raccourcis, dock, tâches, corbeille et plus.",
        "Idioma, atalhos, dock, tarefas, lixeira e mais.");
    public static string ConfigExportSave => T("Export", "Exportar",
        "Exportieren", "Exporter", "Exportar");
    public static string ConfigExportFailed => T(
        "Couldn't save the file. Check that the location can be written to.",
        "No se pudo guardar el archivo. Comprueba que se puede escribir en esa ubicación.",
        "Die Datei konnte nicht gespeichert werden. Prüfe, ob an diesem Ort geschrieben werden darf.",
        "Impossible d'enregistrer le fichier. Vérifiez que l'on peut écrire à cet emplacement.",
        "Não foi possível salvar o arquivo. Verifique se é possível gravar nesse local.");
    public static string ConfigImportTitle => T("Import settings", "Importar configuración",
        "Einstellungen importieren", "Importer la configuration", "Importar configuração");
    public static string ConfigImportNoChanges => T(
        "The file has nothing different from your current settings.",
        "El archivo no trae nada distinto de tu configuración actual.",
        "Die Datei enthält nichts, was von deinen aktuellen Einstellungen abweicht.",
        "Le fichier ne contient rien de différent de votre configuration actuelle.",
        "O arquivo não traz nada diferente da sua configuração atual.");
    public static string ConfigImportSummary(int count) => count == 1
        ? T("This file changes 1 setting:", "Este archivo cambia 1 ajuste:",
            "Diese Datei ändert 1 Einstellung:", "Ce fichier modifie 1 paramètre :", "Este arquivo altera 1 configuração:")
        : T($"This file changes {count} settings:", $"Este archivo cambia {count} ajustes:",
            $"Diese Datei ändert {count} Einstellungen:", $"Ce fichier modifie {count} paramètres :",
            $"Este arquivo altera {count} configurações:");
    public static string ConfigImportApply => T("Apply", "Aplicar",
        "Anwenden", "Appliquer", "Aplicar");
    public static string ConfigImportClose => T("Close", "Cerrar",
        "Schließen", "Fermer", "Fechar");
    public static string ConfigImportRestartForLanguage => T(
        "The language changes when you restart Aldune.",
        "El idioma cambia al reiniciar Aldune.",
        "Die Sprache ändert sich beim Neustart von Aldune.",
        "La langue change au redémarrage d'Aldune.",
        "O idioma muda ao reiniciar o Aldune.");
    public static string ConfigImportBackupNote => T(
        "A copy of your current settings is saved first.",
        "Antes se guarda una copia de tu configuración actual.",
        "Vorher wird eine Kopie deiner aktuellen Einstellungen gespeichert.",
        "Une copie de votre configuration actuelle est d'abord enregistrée.",
        "Antes, uma cópia da sua configuração atual é salva.");
    public static string ConfigImportFromVersion(string app) => T(
        $"File from Aldune {app}", $"Archivo de Aldune {app}",
        $"Datei aus Aldune {app}", $"Fichier d'Aldune {app}", $"Arquivo do Aldune {app}");
    // Genérico a propósito: el detalle que da Core va en español fijo y no es para enseñarlo.
    public static string ConfigImportInvalidFile => T(
        "Couldn't read the file: it isn't a valid Aldune settings file.",
        "No se pudo leer el archivo: no es un archivo de configuración de Aldune válido.",
        "Die Datei konnte nicht gelesen werden: Es ist keine gültige Aldune-Konfigurationsdatei.",
        "Impossible de lire le fichier : ce n'est pas un fichier de configuration Aldune valide.",
        "Não foi possível ler o arquivo: não é um arquivo de configuração do Aldune válido.");
    public static string ConfigImportUnreadable => T(
        "Couldn't open the file. Check that it exists and that you have permission to read it.",
        "No se pudo abrir el archivo. Comprueba que existe y que tienes permiso para leerlo.",
        "Die Datei konnte nicht geöffnet werden. Prüfe, ob sie existiert und du sie lesen darfst.",
        "Impossible d'ouvrir le fichier. Vérifiez qu'il existe et que vous avez le droit de le lire.",
        "Não foi possível abrir o arquivo. Verifique se ele existe e se você tem permissão para lê-lo.");
    public static string ConfigImportBackupFailed => T(
        "Couldn't save a copy of your current settings, so nothing was imported.",
        "No se pudo guardar una copia de tu configuración actual, así que no se ha importado nada.",
        "Es konnte keine Kopie deiner aktuellen Einstellungen gespeichert werden, daher wurde nichts importiert.",
        "Impossible d'enregistrer une copie de votre configuration actuelle : rien n'a été importé.",
        "Não foi possível salvar uma cópia da sua configuração atual, então nada foi importado.");
    public static string ConfigImportSaveFailed => T(
        "The settings were applied but couldn't be saved, so they will be lost when Aldune closes.",
        "Los ajustes se aplicaron pero no se pudieron guardar, así que se perderán al cerrar Aldune.",
        "Die Einstellungen wurden angewendet, konnten aber nicht gespeichert werden und gehen beim Beenden von Aldune verloren.",
        "Les paramètres ont été appliqués mais n'ont pas pu être enregistrés : ils seront perdus à la fermeture d'Aldune.",
        "As configurações foram aplicadas, mas não puderam ser salvas, então serão perdidas ao fechar o Aldune.");
    public static string ConfigValueOn => T("On", "Activado", "Ein", "Activé", "Ativado");
    public static string ConfigValueOff => T("Off", "Desactivado", "Aus", "Désactivé", "Desativado");
    // Una sola fila por atajo en la vista previa (modificadores y tecla juntos).
    public static string ConfigFieldHotkeyCombo => T("New note shortcut (combination)", "Atajo de nota nueva (combinación)",
        "Tastenkürzel für neue Notiz (Kombination)", "Raccourci de nouvelle note (combinaison)", "Atalho de nova nota (combinação)");
    public static string ConfigFieldRecentHotkeyCombo => T("Last note shortcut (combination)", "Atajo de la última nota (combinación)",
        "Tastenkürzel für letzte Notiz (Kombination)", "Raccourci de la dernière note (combinaison)", "Atalho da última nota (combinação)");
    public static string ConfigImportDone => T("Settings imported.", "Configuración importada.",
        "Einstellungen importiert.", "Configuration importée.", "Configuração importada.");

    /// <summary>Nombre visible de cada campo del archivo de configuración (el id es la clave del JSON,
    /// ver ConfigFields). Un id desconocido se devuelve tal cual. Los de atajo llevan además la parte
    /// (modificadores / tecla) para que dos filas seguidas no se llamen igual.</summary>
    public static string ConfigFieldName(string id) => id switch
    {
        "appearance" => T("Appearance", "Aspecto", "Erscheinungsbild", "Apparence", "Aparência"),
        "aspectColors" => T("Aspect colors", "Colores del aspecto", "Farben des Erscheinungsbilds", "Couleurs de l'apparence", "Cores da aparência"),
        "squareCorners" => T("Square corners", "Esquinas rectas", "Eckige Ecken", "Coins carrés", "Cantos retos"),
        "syncSignal" => T("Sync signal", "Señal de sincronización", "Sync-Signal", "Signal de synchronisation", "Sinal de sincronização"),
        "uniformNoteColor" => T("Same color for every note", "Mismo color para todas las notas", "Gleiche Farbe für alle Notizen", "Même couleur pour toutes les notes", "Mesma cor para todas as notas"),
        "noteTheme" => T("Note theme", "Tema de notas", "Notizthema", "Thème des notes", "Tema das notas"),
        "customThemes" => T("Custom themes", "Temas propios", "Eigene Themen", "Thèmes personnalisés", "Temas próprios"),
        "newNoteTone" => T("New note tone", "Tono de las notas nuevas", "Farbton neuer Notizen", "Ton des nouvelles notes", "Tom das notas novas"),
        "colorAssignment" => T("Color for new notes", "Color de las notas nuevas", "Farbe neuer Notizen", "Couleur des nouvelles notes", "Cor das notas novas"),
        "fixedNoteColor" => T("Fixed note color", "Color fijo de las notas", "Feste Notizfarbe", "Couleur fixe des notes", "Cor fixa das notas"),
        "language" => T("Language", "Idioma", "Sprache", "Langue", "Idioma"),
        "simplifiedMode" => T("Simple interface", "Interfaz simple", "Einfache Oberfläche", "Interface simple", "Interface simples"),
        "hotkeyEnabled" => T("New note shortcut", "Atajo de nota nueva", "Tastenkürzel für neue Notiz", "Raccourci de nouvelle note", "Atalho de nova nota"),
        "hotkeyModifiers" => T("New note shortcut (modifier keys)", "Atajo de nota nueva (modificadores)", "Tastenkürzel für neue Notiz (Zusatztasten)", "Raccourci de nouvelle note (touches modificatrices)", "Atalho de nova nota (teclas modificadoras)"),
        "hotkeyKey" => T("New note shortcut (key)", "Atajo de nota nueva (tecla)", "Tastenkürzel für neue Notiz (Taste)", "Raccourci de nouvelle note (touche)", "Atalho de nova nota (tecla)"),
        "recentHotkeyEnabled" => T("Last note shortcut", "Atajo de la última nota", "Tastenkürzel für letzte Notiz", "Raccourci de la dernière note", "Atalho da última nota"),
        "recentHotkeyModifiers" => T("Last note shortcut (modifier keys)", "Atajo de la última nota (modificadores)", "Tastenkürzel für letzte Notiz (Zusatztasten)", "Raccourci de la dernière note (touches modificatrices)", "Atalho da última nota (teclas modificadoras)"),
        "recentHotkeyKey" => T("Last note shortcut (key)", "Atajo de la última nota (tecla)", "Tastenkürzel für letzte Notiz (Taste)", "Raccourci de la dernière note (touche)", "Atalho da última nota (tecla)"),
        "dockEdge" => T("Dock edge", "Borde del dock", "Dock-Rand", "Bord du dock", "Borda do dock"),
        "dockView" => T("Dock view", "Vista del dock", "Dock-Ansicht", "Vue du dock", "Visualização do dock"),
        "keepDockOpen" => T("Keep dock open", "Mantener el dock abierto", "Dock geöffnet lassen", "Garder le dock ouvert", "Manter o dock aberto"),
        "showNotePreview" => T("Note preview", "Vista previa de notas", "Notizvorschau", "Aperçu des notes", "Pré-visualização das notas"),
        "hideOnFullscreen" => T("Hide in fullscreen", "Ocultar a pantalla completa", "Im Vollbild ausblenden", "Masquer en plein écran", "Ocultar em tela cheia"),
        "trackpadGestures" => T("Trackpad gestures", "Gestos del trackpad", "Trackpad-Gesten", "Gestes du pavé tactile", "Gestos do trackpad"),
        "moveCompletedTasksToEnd" => T("Move completed tasks to the end", "Mover las tareas completadas al final", "Erledigte Aufgaben ans Ende verschieben", "Déplacer les tâches terminées à la fin", "Mover as tarefas concluídas para o fim"),
        "autoHideCompletedTasks" => T("Hide completed tasks automatically", "Ocultar tareas completadas automáticamente", "Erledigte Aufgaben automatisch ausblenden", "Masquer automatiquement les tâches terminées", "Ocultar tarefas concluídas automaticamente"),
        "autoHideDelayValue" => T("Hide completed tasks after (amount)", "Ocultar completadas tras (cantidad)", "Erledigte nach (Anzahl) ausblenden", "Masquer les terminées après (quantité)", "Ocultar concluídas após (quantidade)"),
        "autoHideDelayUnit" => T("Hide completed tasks after (unit)", "Ocultar completadas tras (unidad)", "Erledigte nach (Einheit) ausblenden", "Masquer les terminées après (unité)", "Ocultar concluídas após (unidade)"),
        "trashRetentionDays" => T("Days in the trash", "Días en la papelera", "Tage im Papierkorb", "Jours dans la corbeille", "Dias na lixeira"),
        "rememberNotePositions" => T("Remember note positions", "Recordar la posición de las notas", "Notizpositionen merken", "Mémoriser la position des notes", "Lembrar a posição das notas"),
        "checkForUpdates" => T("Check for updates automatically", "Buscar actualizaciones automáticamente", "Automatisch nach Updates suchen", "Rechercher les mises à jour automatiquement", "Verificar atualizações automaticamente"),
        _ => id,
    };

    public static string UpdateChecking => T("Checking for updates…", "Buscando actualizaciones…",
        "Nach Updates wird gesucht…", "Recherche de mises à jour…", "Verificando atualizações…");
    public static string UpdateCurrentMessage => T("Aldune is up to date.", "Aldune está actualizado.",
        "Aldune ist auf dem neuesten Stand.", "Aldune est à jour.", "Aldune está atualizado.");
    public static string UpdateErrorMessage => T(
        "The update check failed. Try again later.",
        "No se ha podido comprobar si hay actualizaciones. Inténtalo más tarde.",
        "Die Update-Prüfung ist fehlgeschlagen. Versuche es später erneut.",
        "Échec de la recherche de mises à jour. Réessayez plus tard.",
        "Não foi possível verificar atualizações. Tente novamente mais tarde.");
    public static string UpdateAvailableMessage(string version) =>
        T($"Aldune {version} is available.",
          $"Aldune {version} está disponible.",
            $"Aldune {version} ist verfügbar.",
            $"Aldune {version} est disponible.",
            $"Aldune {version} está disponível.");
    public static string UpdateAvailableTitle => T("Update available", "Actualización disponible",
        "Update verfügbar", "Mise à jour disponible", "Atualização disponível");
    public static string UpdateDownload => T("Download", "Descargar",
        "Herunterladen", "Télécharger", "Baixar");
    public static string UpdateLater => T("Later", "Más tarde",
        "Später", "Plus tard", "Mais tarde");
    public static string UpdateWindowTitle => T("Aldune update", "Actualización de Aldune",
        "Aldune-Update", "Mise à jour d'Aldune", "Atualização do Aldune");
    public static string UpdateOpenErrorMessage => T(
        "The download page couldn't be opened.",
        "No se ha podido abrir la página de descarga.",
        "Die Download-Seite konnte nicht geöffnet werden.",
        "Impossible d'ouvrir la page de téléchargement.",
        "Não foi possível abrir a página de download.");
    public static string ReminderMenuEntry => T("Reminder", "Recordatorio",
        "Erinnerung", "Rappel", "Lembrete");
    public static string ReminderSet(DateTimeOffset dueAt) =>
        T($"Reminder: {dueAt:dd/MM HH:mm}", $"Recordatorio: {dueAt:dd/MM HH:mm}",
            $"Erinnerung: {dueAt:dd/MM HH:mm}",
            $"Rappel : {dueAt:dd/MM HH:mm}",
            $"Lembrete: {dueAt:dd/MM HH:mm}");
    public static string ReminderInOneHour => T("In 1 hour", "En 1 hora",
        "In 1 Stunde", "Dans 1 heure", "Em 1 hora");
    public static string ReminderTonight => T("Tonight", "Esta noche",
        "Heute Abend", "Ce soir", "Hoje à noite");
    public static string ReminderTomorrowMorning => T("Tomorrow 9:00", "Mañana 9:00",
        "Morgen 9:00", "Demain 9:00", "Amanhã às 9:00");
    public static string ReminderSave => T("Save", "Guardar",
        "Speichern", "Enregistrer", "Salvar");
    public static string ReminderClear => T("Remove reminder", "Quitar recordatorio",
        "Erinnerung entfernen", "Supprimer le rappel", "Remover lembrete");
    public static string PinnedOn => T("✓  Always on top", "✓  Siempre encima",
        "✓  Immer im Vordergrund", "✓  Toujours au premier plan", "✓  Sempre no topo");
    public static string PinnedOff => T("Always on top", "Siempre encima",
        "Immer im Vordergrund", "Toujours au premier plan", "Sempre no topo");
    public static string PinnedOnHint => T("The note stays in front of other windows.", "La nota se queda por delante de las demás ventanas.",
        "Die Notiz bleibt vor anderen Fenstern.",
        "La note reste devant les autres fenêtres.",
        "A nota fica à frente de outras janelas.");
    public static string PinnedOffHint => T("The note goes behind when you click another window.", "La nota se queda detrás al pinchar en otra ventana.",
        "Die Notiz tritt in den Hintergrund, wenn du auf ein anderes Fenster klickst.",
        "La note passe à l'arrière-plan lorsque vous cliquez sur une autre fenêtre.",
        "A nota vai para trás quando você clica em outra janela.");

    // --- Dock (mazo anclado al borde) -------------------------------------------------------------

    public static string ManageNotesTooltip => T("Manage notes", "Gestionar notas",
        "Notizen verwalten", "Gérer les notes", "Gerenciar notas");
    public static string ManageNotesTaggedTooltip(string tag) => T(
        $"Manage the notes with the tag \"{tag}\"", $"Gestionar las notas con la etiqueta \"{tag}\"",
        $"Notizen mit dem Tag \"{tag}\" verwalten",
        $"Gérer les notes avec l'étiquette \"{tag}\"",
        $"Gerenciar as notas com a etiqueta \"{tag}\"");
    public static string OpenAllNotesTooltip => T("Open all notes (closes them all if they're already open)", "Abrir todas las notas (las cierra todas si ya están abiertas)",
        "Alle Notizen öffnen (schließt alle, wenn sie bereits geöffnet sind)",
        "Ouvrir toutes les notes (les ferme toutes si elles sont déjà ouvertes)",
        "Abrir todas as notas (fecha todas se já estiverem abertas)");
    public static string OpenAllMenuTitle => T("Open all with...", "Abrir todas con...",
        "Alle öffnen mit...", "Tout ouvrir avec...", "Abrir todas com...");
    public static string OpenAllModeTitle => T("Opening mode", "Modo de apertura",
        "Öffnungsmodus", "Mode d'ouverture", "Modo de abertura");
    public static string OpenAllMonitorTitle => T("Open notes on", "Abrir las notas en",
        "Notizen öffnen auf", "Ouvrir les notes sur", "Abrir notas em");
    public static string CloseAllNotes => T("Close all notes", "Cerrar todas las notas",
        "Alle Notizen schließen", "Fermer toutes les notes", "Fechar todas as notas");
    public static string OpenAllGrid => T("Grid", "Cuadrícula",
        "Raster", "Grille", "Grade");
    public static string OpenAllColumns => T("Columns", "Columnas",
        "Spalten", "Colonnes", "Colunas");
    public static string CascadeNearDock => T("Cascade by the dock", "Cascada junto al dock",
        "Kaskadierend am Dock", "En cascade près du dock", "Cascata junto ao dock");
    public static string TrayToggleDock => T("Hide the dock", "Ocultar el dock",
        "Dock ausblenden", "Masquer le dock", "Ocultar o dock");
    public static string TrayShowDock => T("Show the dock", "Mostrar el dock",
        "Dock einblenden", "Afficher le dock", "Mostrar o dock");
    public static string TrackpadGesturesCheckbox => T("Trackpad gestures",
        "Gestos de trackpad",
        "Touchpad-Gesten", "Gestes du pavé tactile", "Gestos do touchpad");
    public static string TrackpadGesturesHint => T(
        "Scrolls with the trackpad are read as continuous gestures instead of mouse notches, so two fingers move the list in proportion instead of jumping. Only available when a precision trackpad is detected.",
        "Los deslizamientos del trackpad se leen como gestos continuos en vez de muescas de ratón, así que dos dedos mueven la lista en proporción en vez de a saltos. Solo está disponible cuando se detecta un trackpad de precisión.",
        "Das Scrollen mit dem Touchpad wird als kontinuierliche Geste statt in Mausrad-Schritten erfasst, sodass zwei Finger die Liste gleichmäßig statt sprunghaft bewegen. Nur verfügbar, wenn ein Präzisions-Touchpad erkannt wird.",
        "Les défilements au pavé tactile sont interprétés comme des gestes continus et non des crans de souris, de sorte que deux doigts déplacent la liste proportionnellement sans sauts. Disponible uniquement lorsqu'un pavé tactile de précision est détecté.",
        "A rolagem com o touchpad é lida como gestos contínuos em vez de etapas do mouse, permitindo que dois dedos movam a lista suavemente em vez de saltos. Disponível apenas quando um touchpad de precisão for detectado.");
    public static string TrackpadGesturesNotFound => T(
        "No precision trackpad detected on this PC, so it can't be turned on.",
        "No se ha detectado un trackpad de precisión en este equipo, así que no se puede activar.",
        "Auf diesem PC wurde kein Präzisions-Touchpad erkannt, daher kann es nicht aktiviert werden.",
        "Aucun pavé tactile de précision n'a été détecté sur ce PC, impossible de l'activer.",
        "Nenhum touchpad de precisão detectado neste PC, portanto não pode ser ativado.");
    public static string NewNoteTooltip => T("New note", "Nueva nota",
        "Neue Notiz", "Nouvelle note", "Nova nota");

    // Segunda línea de los tooltips del pie: los cuatro botones tienen clic derecho y nada lo decía.
    public static string OpenAllRightClickHint => T("Right-click: layout and screen", "Clic derecho: disposición y pantalla",
        "Rechtsklick: Layout und Bildschirm",
        "Clic droit : disposition et écran",
        "Clique direito: layout e tela");
    public static string ManageNotesRightClickHint => T("Right-click: view and tags", "Clic derecho: vista y etiquetas",
        "Rechtsklick: Ansicht und Tags",
        "Clic droit : affichage et étiquettes",
        "Clique direito: exibição e etiquetas");
    public static string SyncRightClickHint => T("Right-click: sync settings", "Clic derecho: ajustes de sincronización",
        "Rechtsklick: Synchronisierungseinstellungen",
        "Clic droit : paramètres de synchronisation",
        "Clique direito: configurações de sincronização");
    /// <summary>Carpeta de la línea de prompt de la piel bash (<c>usuario@aldune:~/notas$</c>).</summary>
    public static string PromptNotesFolder => T("notes", "notas", "notizen", "notes", "notas");
    public static string NewNoteRightClickHint => T("Right-click: from the clipboard", "Clic derecho: desde el portapapeles",
        "Rechtsklick: aus der Zwischenablage",
        "Clic droit : depuis le presse-papiers",
        "Clique direito: da área de transferência");
    public static string WithHint(string tooltip, string hint) => tooltip + Environment.NewLine + hint;
    public static string NewNoteFromClipboard => T("New note from clipboard", "Nueva nota desde el portapapeles",
        "Neue Notiz aus der Zwischenablage",
        "Nouvelle note depuis le presse-papiers",
        "Nova nota da área de transferência");
    public static string KeepDockOpenOn => T("✓  Keep dock open", "✓  Mantener el dock abierto",
        "✓  Dock geöffnet lassen", "✓  Garder le dock ouvert", "✓  Manter o dock aberto");
    public static string KeepDockOpenOff => T("Keep dock open", "Mantener el dock abierto",
        "Dock geöffnet lassen", "Garder le dock ouvert", "Manter o dock aberto");
    public static string NewNoteTaggedTooltip(string tag) => T(
        $"New note with the tag \"{tag}\"", $"Nueva nota con la etiqueta \"{tag}\"",
        $"Neue Notiz mit dem Tag \"{tag}\"",
        $"Nouvelle note avec l'étiquette \"{tag}\"",
        $"Nova nota com a etiqueta \"{tag}\"");
    public static string OpenAllNotesTaggedTooltip(string tag) => T(
        $"Open the notes with the tag \"{tag}\" (closes them if they're already open)",
        $"Abrir las notas con la etiqueta \"{tag}\" (las cierra si ya están abiertas)",
        $"Notizen mit dem Tag \"{tag}\" öffnen (schließt sie, falls sie bereits geöffnet sind)",
        $"Ouvrir les notes avec l'étiquette \"{tag}\" (les ferme si elles sont déjà ouvertes)",
        $"Abrir as notas com a etiqueta \"{tag}\" (fecha se já estiverem abertas)");
    public static string ExitAppTooltip => T("Exit Aldune", "Salir de Aldune",
        "Aldune beenden", "Quitter Aldune", "Sair do Aldune");
    public static string ScrollNotesUpTooltip => T("Show earlier notes", "Ver notas anteriores",
        "Vorherige Notizen anzeigen", "Afficher les notes précédentes", "Ver notas anteriores");
    public static string ScrollNotesDownTooltip => T("Show later notes", "Ver notas siguientes",
        "Nächste Notizen anzeigen", "Afficher les notes suivantes", "Ver notas seguintes");
    public static string OpenNote => T("Open", "Abrir",
        "Öffnen", "Ouvrir", "Abrir");
    public static string ProtectNote => T("Protect with password…", "Proteger con contraseña…",
        "Mit Passwort schützen…", "Protéger par mot de passe…", "Proteger com senha…");
    public static string RemoveProtection => T("Remove password protection", "Quitar protección con contraseña",
        "Passwortschutz aufheben", "Supprimer la protection par mot de passe", "Remover proteção por senha");
    public static string UnlockNote => T("Unlock note", "Desbloquear nota",
        "Notiz entsperren", "Déverrouiller la note", "Desbloquear nota");
    public static string ProtectedNote => T("Protected note", "Nota protegida",
        "Geschützte Notiz", "Note protégée", "Nota protegida");
    public static string ProtectedNoteHint => T("Enter the password to open this note. It is not stored by Aldune.", "Escribe la contraseña para abrir esta nota. Aldune no la guarda.",
        "Gib das Passwort ein, um diese Notiz zu öffnen. Es wird von Aldune nicht gespeichert.",
        "Entrez le mot de passe pour ouvrir cette note. Il n'est pas enregistré par Aldune.",
        "Digite a senha para abrir esta nota. Ela não é salva pelo Aldune.");
    public static string ProtectNoteHint => T("Choose a password for this note. If you lose it, the note cannot be recovered.", "Elige una contraseña para esta nota. Si la pierdes, no se podrá recuperar.",
        "Wähle ein Passwort für diese Notiz. Wenn du es verlierst, kann die Notiz nicht wiederhergestellt werden.",
        "Choisissez un mot de passe pour cette note. Si vous le perdez, la note ne pourra pas être récupérée.",
        "Escolha uma senha para esta nota. Se você a perder, a nota não poderá ser recuperada.");
    public static string PasswordLabel => T("Password", "Contraseña",
        "Passwort", "Mot de passe", "Senha");
    public static string ConfirmPasswordLabel => T("Repeat password", "Repite la contraseña",
        "Passwort wiederholen", "Répétez le mot de passe", "Repita a senha");
    public static string PasswordTooShort => T("Use at least 4 characters.", "Usa al menos 4 caracteres.",
        "Verwende mindestens 4 Zeichen.", "Utilisez au moins 4 caractères.", "Use pelo menos 4 caracteres.");
    public static string PasswordsDoNotMatch => T("The passwords do not match.", "Las contraseñas no coinciden.",
        "Die Passwörter stimmen nicht überein.",
        "Les mots de passe ne correspondent pas.",
        "As senhas não coincidem.");
    public static string WrongPassword => T("That password is not correct.", "Esa contraseña no es correcta.",
        "Dieses Passwort ist nicht korrekt.",
        "Ce mot de passe est incorrect.",
        "Essa senha não está correta.");
    public static string Accept => T("Accept", "Aceptar",
        "Bestätigen", "Accepter", "Aceitar");
    public static string ProtectNoteTitle => T("Protect with a password", "Proteger con contraseña",
        "Mit einem Passwort schützen", "Protéger par un mot de passe", "Proteger com uma senha");
    public static string ProtectAction => T("Protect", "Proteger",
        "Schützen", "Protéger", "Proteger");
    public static string UnlockAction => T("Unlock", "Desbloquear",
        "Entsperren", "Déverrouiller", "Desbloquear");
    public static string RemoveProtectionTitle => T("Remove protection", "Quitar la protección",
        "Schutz aufheben", "Supprimer la protection", "Remover a proteção");
    public static string RemoveProtectionHint => T("Enter this note's password. It will stay encrypted with Aldune's own key, like any other note.", "Escribe la contraseña de esta nota. Seguirá cifrada con la clave de Aldune, como el resto de notas.",
        "Gib das Passwort dieser Notiz ein. Sie bleibt mit dem eigenen Schlüssel von Aldune verschlüsselt, wie jede andere Notiz auch.",
        "Entrez le mot de passe de cette note. Elle restera chiffrée avec la clé propre d'Aldune, comme toute autre note.",
        "Digite a senha desta nota. Ela continuará criptografada com a própria chave do Aldune, como qualquer outra nota.");
    public static string RemoveProtectionAction => T("Remove protection", "Quitar protección",
        "Schutz aufheben", "Supprimer la protection", "Remover proteção");
    public static string PasswordEmpty => T("Type the password.", "Escribe la contraseña.",
        "Gib das Passwort ein.", "Tapez le mot de passe.", "Digite a senha.");
    public static string ShowPassword => T("Show password", "Mostrar contraseña",
        "Passwort anzeigen", "Afficher le mot de passe", "Mostrar senha");
    public static string HidePassword => T("Hide password", "Ocultar contraseña",
        "Passwort ausblenden", "Masquer le mot de passe", "Ocultar senha");
    public static string CapsLockOn => T("Caps Lock is on.", "Bloq Mayús está activado.",
        "Feststelltaste ist aktiviert.", "Verr Maj est activé.", "O Caps Lock está ativado.");
    public static string DockViewTooltip => T("Dock view", "Vista del dock",
        "Dock-Ansicht", "Vue du dock", "Exibição do dock");
    public static string DockViewTitle => T("Show in dock", "Mostrar en el dock",
        "Im Dock anzeigen", "Afficher dans le dock", "Mostrar no dock");
    public static string DockViewActive => T("Active notes", "Notas activas",
        "Aktive Notizen", "Notes actives", "Notas ativas");
    public static string DockViewArchived => T("Archived notes", "Notas archivadas",
        "Archivierte Notizen", "Notes archivées", "Notas arquivadas");
    public static string DockViewTrash => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira");
    public static string DockViewTags => T("By tag", "Por etiqueta",
        "Nach Tag", "Par étiquette", "Por etiqueta");
    public static string DockViewAllTags => T("Choose a tag", "Elegir una etiqueta",
        "Tag auswählen", "Choisir une étiquette", "Escolher uma etiqueta");
    public static string DockViewNoTags => T("No tags yet", "Aún no hay etiquetas",
        "Noch keine Tags", "Aucune étiquette pour l'instant", "Nenhuma etiqueta ainda");
    public static string NoteTags => T("Tags…", "Etiquetas…",
        "Tags…", "Étiquettes…", "Etiquetas…");
    public static string AddTags => T("+ Tags", "+ Etiquetas",
        "+ Tags", "+ Étiquettes", "+ Etiquetas");
    public static string EditTagsTooltip => T("Edit tags", "Editar etiquetas",
        "Tags bearbeiten", "Modifier les étiquettes", "Editar etiquetas");
    public static string SortTooltip => T("Sort", "Ordenar",
        "Sortieren", "Trier", "Ordenar");
    public static string SortDock => T("Dock order", "Orden del mazo",
        "Dock-Reihenfolge", "Ordre du dock", "Ordem do dock");
    public static string SortNewest => T("Recent first", "Más recientes",
        "Neueste zuerst", "Les plus récents d'abord", "Mais recentes primeiro");
    public static string SortOldest => T("Oldest first", "Más antiguas",
        "Älteste zuerst", "Les plus anciens d'abord", "Mais antigas primeiro");
    public static string SortTitle => T("Title (A–Z)", "Título (A-Z)",
        "Titel (A–Z)", "Titre (A–Z)", "Título (A–Z)");
    public static string SyncInsecureUrlWarning => T(
        "This address uses http:// outside your local network: your notes stay encrypted, but the access token or password travels unprotected. Use https://.",
        "Esta dirección usa http:// fuera de tu red local: las notas siguen cifradas, pero el token o la contraseña viajan sin protección. Usa https://.",
        "Diese Adresse verwendet http:// außerhalb deines lokalen Netzwerks: Die Notizen bleiben verschlüsselt, aber das Zugriffstoken oder Passwort wird ungeschützt übertragen. Verwende https://.",
        "Cette adresse utilise http:// en dehors de ton réseau local : vos notes restent chiffrées, mais le jeton d'accès ou le mot de passe circule sans protection. Utilisez https://.",
        "Este endereço usa http:// fora da sua rede local: suas notas permanecem criptografadas, mas o token de acesso ou a senha trafegam sem proteção. Use https://.");
    public static string WelcomeTitle => T("Aldune is ready", "Aldune está lista",
        "Aldune ist bereit", "Aldune est prêt", "Aldune está pronto");
    public static string WelcomeMessage(Aldune.Core.EdgePosition edge, string? hotkey)
    {
        var (en, es) = edge switch
        {
            Aldune.Core.EdgePosition.Left => ("left", "izquierdo"),
            Aldune.Core.EdgePosition.Top => ("top", "superior"),
            Aldune.Core.EdgePosition.Bottom => ("bottom", "inferior"),
            _ => ("right", "derecho"),
        };
        return hotkey is null
            ? T($"Your notes live on the {en} edge of the screen: move the mouse there and click + to create one.",
                $"Tus notas viven en el borde {es} de la pantalla: lleva el ratón ahí y pulsa + para crear una.",
                $"Deine Notizen leben am {en} Bildschirmrand: Bewege die Maus dorthin und klicke auf +, um eine zu erstellen.",
                $"Vos notes se trouvent sur le bord {en} de l'écran : déplacez la souris là et cliquez sur + pour en créer une.",
                $"Suas notas ficam na borda {en} da tela: mova o mouse até lá e clique em + para criar uma.")
            : T($"Your notes live on the {en} edge of the screen: move the mouse there, or press {hotkey} to create one.",
                $"Tus notas viven en el borde {es} de la pantalla: lleva el ratón ahí, o pulsa {hotkey} para crear una.",
                $"Deine Notizen leben am {en} Bildschirmrand: Bewege die Maus dorthin oder drücke {hotkey}, um eine zu erstellen.",
                $"Vos notes se trouvent sur le bord {en} de l'écran : déplacez la souris là ou appuyez sur {hotkey} pour en créer une.",
                $"Suas notas ficam na borda {en} da tela: mova o mouse até lá ou pressione {hotkey} para criar uma.");
    }
    public static string TagPanelTitle => T("Tags", "Etiquetas",
        "Tags", "Tags", "Tags");
    public static string TagPanelHint => T("Changes are saved as you tick them.", "Se guardan al marcarlas.",
        "Die Änderungen werden beim Abhaken gespeichert.",
        "Les modifications sont enregistrées au fur et à mesure.",
        "As alterações são salvas ao marcá-las.");
    public static string TagPanelEmpty => T("No tags yet. Create the first one below.", "Aún no hay etiquetas. Crea la primera aquí debajo.",
        "Noch keine Tags. Erstelle unten den ersten.",
        "Aucun tag pour l'instant. Créez le premier ci-dessous.",
        "Nenhuma tag ainda. Crie a primeira abaixo.");
    public static string NewTagPlaceholder => T("New tag", "Nueva etiqueta",
        "Neuer Tag", "Nouveau tag", "Nova tag");
    public static string ManageTags => T("Manage tags", "Gestionar etiquetas",
        "Tags verwalten", "Gérer les tags", "Gerenciar tags");
    public static string NewTag => T("New tag", "Nueva etiqueta",
        "Neuer Tag", "Nouveau tag", "Nova tag");
    public static string CreateTag => T("Create", "Crear",
        "Erstellen", "Créer", "Criar");
    public static string DeleteTag => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir");
    public static string DeleteTagTooltip => T("Delete tag", "Eliminar etiqueta",
        "Tag löschen", "Supprimer le tag", "Excluir tag");
    public static string AllTags => T("All tags", "Todas las etiquetas",
        "Alle Tags", "Tous les tags", "Todas as tags");
    public static string TagFilterTooltip => T("Filter by tag", "Filtrar por etiqueta",
        "Nach Tag filtern", "Filtrer par tag", "Filtrar por tag");
    public static string NoTagResults(string tag) =>
        T($"No notes have the tag “{tag}”.", $"Ninguna nota tiene la etiqueta «{tag}».",
            $"Keine Notiz hat den Tag „{tag}“.",
            $"Aucune note n'a le tag « {tag} ».",
            $"Nenhuma nota tem a tag “{tag}”.");

    // --- Gestor de notas ---------------------------------------------------------------------------

    public static string ManageNotesTitle => T("Manage notes", "Gestionar notas",
        "Notizen verwalten", "Gérer les notes", "Gerenciar notas");
    public static string SettingsTooltip => T("Settings", "Ajustes",
        "Einstellungen", "Paramètres", "Configurações");
    public static string FilterActive => T("Active", "Activas",
        "Aktiv", "Actives", "Ativas");
    public static string FilterArchived => T("Archived", "Archivadas",
        "Archiviert", "Archivées", "Arquivadas");
    public static string FilterTrashed => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira");
    public static string SearchPlaceholder => T("Search your notes…", "Buscar en tus notas…",
        "Notizen durchsuchen…", "Rechercher dans vos notes…", "Pesquisar nas suas notas…");
    public static string ClearSearchTooltip => T("Clear search", "Borrar búsqueda",
        "Suche löschen", "Effacer la recherche", "Limpar pesquisa");
    public static string SelectAll => T("Select all", "Seleccionar todo",
        "Alle auswählen", "Tout sélectionner", "Selecionar tudo");
    public static string Delete => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir");
    public static string DeletePermanentlyTitle => T("Delete permanently", "Eliminar definitivamente",
        "Endgültig löschen", "Supprimer définitivement", "Excluir permanentemente");
    public static string Export => T("Export", "Exportar",
        "Exportieren", "Exporter", "Exportar");
    public static string ExportFolderDialogTitle => T("Choose a folder to export to", "Elige una carpeta donde exportar",
        "Ordner zum Exportieren wählen",
        "Choisir un dossier d'exportation",
        "Escolha uma pasta para exportar");
    public static string ExportFormatTitle => T("Export notes", "Exportar notas",
        "Notizen exportieren", "Exporter les notes", "Exportar notas");
    public static string ExportAsZipPrompt => T(
        "Export as a single .zip file?\n\nChoose “No” to get loose .md files in a folder instead.",
        "¿Exportar como un único archivo .zip?\n\nElige «No» para tener los archivos .md sueltos en una carpeta.",
        "Als einzelne .zip-Datei exportieren?\n\nWähle „Nein“, um stattdessen lose .md-Dateien in einem Ordner zu erhalten.",
        "Exporter en un seul fichier .zip ?\n\nChoisissez « Non » pour obtenir des fichiers .md séparés dans un dossier.",
        "Exportar como um único arquivo .zip?\n\nEscolha “Não” para obter arquivos .md soltos em uma pasta.");
    public static string ZipFileFilter => T("Zip file (*.zip)|*.zip", "Archivo zip (*.zip)|*.zip",
        "Zip-Datei (*.zip)|*.zip", "Fichier zip (*.zip)|*.zip", "Arquivo zip (*.zip)|*.zip");

    public static string OneNote => T("1 note", "1 nota",
        "1 Notiz", "1 note", "1 nota");
    public static string NotesCount(int count) => T($"{count} notes", $"{count} notas",
        $"{count} Notizen", $"{count} notes", $"{count} notas");
    public static string OneSelected => T("1 selected", "1 seleccionada",
        "1 ausgewählt", "1 sélectionnée", "1 selecionada");
    public static string SelectedCount(int count) => T($"{count} selected", $"{count} seleccionadas",
        $"{count} ausgewählt", $"{count} sélectionnées", $"{count} selecionadas");

    public static string NoSearchResults(string query) =>
        T($"No note contains “{query}”.", $"Ninguna nota contiene «{query}».",
            $"Keine Notiz enthält „{query}“.",
            $"Aucune note ne contient « {query} ».",
            $"Nenhuma nota contém “{query}”.");
    public static string EmptyActive => T("No active notes. Create one with the + button on the screen edge.", "No hay notas activas. Crea una con el botón + del borde de la pantalla.",
        "Keine aktiven Notizen. Erstelle eine mit dem +-Knopf am Bildschirmrand.",
        "Aucune note active. Créez-en une avec le bouton + sur le bord de l'écran.",
        "Nenhuma nota ativa. Crie uma com o botão + na borda da tela.");
    public static string EmptyArchived => T("You haven't archived any notes yet.", "No has archivado ninguna nota todavía.",
        "Du hast noch keine Notizen archiviert.",
        "Vous n'avez pas encore archivé de notes.",
        "Você ainda não arquivou nenhuma nota.");
    public static string EmptyTrashed => T("Trash is empty. Anything you send here is deleted after 30 days.", "La papelera está vacía. Lo que envíes aquí se borra solo a los 30 días.",
        "Der Papierkorb ist leer. Alles, was du hierher sendest, wird nach 30 Tagen gelöscht.",
        "La corbeille est vide. Tout ce que vous y envoyez est supprimé après 30 jours.",
        "A lixeira está vazia. Tudo que você enviar aqui será excluído após 30 dias.");
    public static string EmptyNone => T("There are no notes yet. Create one with the + button on the screen edge.", "Todavía no hay notas. Crea una con el botón + del borde de la pantalla.",
        "Es gibt noch keine Notizen. Erstelle eine mit dem +-Knopf am Bildschirmrand.",
        "Aucune note pour l'instant. Créez-en une avec le bouton + sur le bord de l'écran.",
        "Ainda não há notas. Crie uma com o botão + na borda da tela.");

    public static string ConfirmDeleteOne => T("1 note will be permanently deleted. This action cannot be undone.", "Se eliminará 1 nota definitivamente. Esta acción no se puede deshacer.",
        "1 Notiz wird endgültig gelöscht. Diese Aktion kann nicht rückgängig gemacht werden.",
        "1 note sera supprimée définitivement. Cette action est irréversible.",
        "1 nota será excluída permanentemente. Esta ação não pode ser desfeita.");
    public static string ConfirmDeleteMany(int count) =>
        T($"{count} notes will be permanently deleted. This action cannot be undone.",
          $"Se eliminarán {count} notas definitivamente. Esta acción no se puede deshacer.",
            $"{count} Notizen werden endgültig gelöscht. Diese Aktion kann nicht rückgängig gemacht werden.",
            $"{count} notes seront supprimées définitivement. Cette action est irréversible.",
            $"{count} notas serão excluídas permanentemente. Esta ação não pode ser desfeita.");

    // --- Bandeja del sistema ------------------------------------------------------------------------

    public static string TrayNewNote => T("New note", "Nueva nota",
        "Neue Notiz", "Nouvelle note", "Nova nota");
    public static string TrayManageNotes => T("Manage notes…", "Gestionar notas…",
        "Notizen verwalten…", "Gérer les notes…", "Gerenciar notas…");
    public static string TraySettings => T("Settings…", "Ajustes…",
        "Einstellungen…", "Paramètres…", "Configurações…");
    public static string TrayExit => T("Exit", "Salir",
        "Beenden", "Quitter", "Sair");

    // --- Ventana de Ajustes -------------------------------------------------------------------------

    public static string SettingsWindowTitle => T("Aldune settings", "Ajustes de Aldune",
        "Aldune-Einstellungen", "Paramètres d'Aldune", "Configurações do Aldune");
    public static string SettingsHeader => T("Settings", "Ajustes",
        "Einstellungen", "Paramètres", "Configurações");
    public static string InterfaceModeSectionTitle => T("Interface mode", "Modo de interfaz",
        "Oberflächenmodus", "Mode d'interface", "Modo de interface");
    public static string InterfaceModeHint => T(
        "The simplified mode keeps the essentials and hides advanced options. You can switch back at any time.",
        "El modo simplificado conserva lo esencial y oculta las opciones avanzadas. Puedes volver al modo completo cuando quieras.",
        "Der vereinfachte Modus behält das Wesentliche und blendet erweiterte Optionen aus. Du kannst jederzeit zurückkehren.",
        "Le mode simplifié conserve l'essentiel et masque les options avancées. Vous pouvez revenir au mode complet à tout moment.",
        "O modo simplificado mantém o essencial e oculta as opções avançadas. Você pode voltar ao modo completo a qualquer momento.");
    public static string SwitchToSimplifiedMode => T("Use simplified mode", "Usar modo simplificado",
        "Vereinfachten Modus verwenden", "Utiliser le mode simplifié", "Usar modo simplificado");
    public static string SwitchToCompleteMode => T("Use complete mode", "Usar modo completo",
        "Vollständigen Modus verwenden", "Utiliser le mode complet", "Usar modo completo");
    public static string ApplicationSectionTitle => T("Application", "Aplicación",
        "Anwendung", "Application", "Aplicativo");
    public static string RestartApplicationButton => T("Restart Aldune", "Reiniciar Aldune",
        "Aldune neu starten", "Redémarrer Aldune", "Reiniciar Aldune");
    public static string RestartAppTooltip => T("Restart Aldune", "Reiniciar Aldune",
        "Aldune neu starten", "Redémarrer Aldune", "Reiniciar Aldune");
    public static string ExitApplicationButton => T("Exit Aldune", "Salir de Aldune",
        "Aldune beenden", "Quitter Aldune", "Fechar Aldune");

    public static string StartupCheckbox => T("Open Aldune at sign-in", "Abrir Aldune al iniciar sesión",
        "Aldune bei der Anmeldung öffnen", "Ouvrir Aldune à la connexion", "Abrir Aldune ao fazer login");
    public static string StartupHint => T("You can also remove it from Task Manager, in the Startup tab.", "También puedes quitarlo desde Administrador de tareas, en la pestaña Inicio.",
        "Du kannst es auch über den Task-Manager entfernen, auf der Registerkarte Autostart.",
        "Vous pouvez aussi le supprimer depuis le Gestionnaire des tâches, dans l'onglet Démarrage.",
        "Você também pode removê-lo pelo Gerenciador de Tarefas, na guia Inicializar.");

    public static string RecentNoteHotkeyCheckbox => T("Open the last edited note with a keyboard shortcut",
        "Abrir la última nota editada con un atajo de teclado",
        "Zuletzt bearbeitete Notiz mit Tastenkombination öffnen",
        "Ouvrir la dernière note modifiée avec un raccourci clavier",
        "Abrir a última nota editada com um atalho de teclado");

    public static string HotkeyCheckbox => T("Create a note with a keyboard shortcut", "Crear una nota con un atajo de teclado",
        "Notiz mit Tastenkombination erstellen",
        "Créer une note avec un raccourci clavier",
        "Criar uma nota com atalho de teclado");
    public static string HotkeyResetButton => T("Reset", "Restablecer",
        "Zurücksetzen", "Réinitialiser", "Redefinir");
    public static string HotkeyRecording => T("Press a key combination… (Esc to cancel)", "Pulsa una combinación… (Esc para cancelar)",
        "Tastenkombination drücken… (Esc zum Abbrechen)",
        "Appuyez sur une combinaison… (Esc pour annuler)",
        "Pressione uma combinação… (Esc para cancelar)");
    public static string HotkeyNeedsModifier => T("Add at least Ctrl, Alt, Shift or Win to the combination.", "Añade al menos Ctrl, Alt, Shift o Win a la combinación.",
        "Füge mindestens Ctrl, Alt, Shift oder Win zur Kombination hinzu.",
        "Ajoutez au moins Ctrl, Alt, Shift ou Win à la combinaison.",
        "Adicione pelo menos Ctrl, Alt, Shift ou Win à combinação.");
    public static string HotkeyDisabled => T("Disabled.", "Desactivado.",
        "Deaktiviert.", "Désactivé.", "Desativado.");
    public static string HotkeyWorks => T("Works from any application, without going to the screen edge.", "Funciona desde cualquier aplicación, sin tener que ir al borde de la pantalla.",
        "Funktioniert aus jeder Anwendung heraus, ohne zum Bildschirmrand gehen zu müssen.",
        "Fonctionne depuis n'importe quelle application, sans aller au bord de l'écran.",
        "Funciona de qualquer aplicativo, sem precisar ir até a borda da tela.");
    public static string HotkeyConflict(string combo) =>
        T($"Couldn't activate it: another application already uses {combo}. Choose a different combination.",
          $"No se ha podido activar: otra aplicación ya usa {combo}. Elige otra combinación.",
            $"Konnte nicht aktiviert werden: Eine andere Anwendung verwendet bereits {combo}. Wähle eine andere Kombination.",
            $"Impossible d'activer : une autre application utilise déjà {combo}. Choisissez une autre combinaison.",
            $"Não foi possível ativar: outro aplicativo já usa {combo}. Escolha uma combinação diferente.");

    public static string MonitorSectionTitle => T("Screens where Aldune shows", "Pantallas donde mostrar Aldune",
        "Bildschirme, auf denen Aldune angezeigt wird",
        "Écrans où Aldune s'affiche",
        "Telas onde o Aldune é exibido");
    public static string MonitorSectionHint => T("Choose whether to show Aldune's dock on every screen or restrict it to one.", "Elige si deseas ver el dock de Aldune en todas las pantallas o restringirlo a una específica.",
        "Wähle, ob das Aldune-Dock auf jedem Bildschirm angezeigt oder auf einen bestimmten beschränkt werden soll.",
        "Choisissez si vous souhaitez afficher le dock Aldune sur chaque écran ou le limiter à un seul.",
        "Escolha se deseja mostrar o dock do Aldune em todas as telas ou restringi-lo a uma específica.");
    public static string AllScreens => T("On every connected screen", "En todas las pantallas conectadas",
        "Auf jedem angeschlossenen Bildschirm", "Sur chaque écran connecté", "Em todas as telas conectadas");
    public static string MouseScreen => T("On the screen with the mouse", "En la pantalla donde esté el ratón",
        "Auf dem Bildschirm mit der Maus",
        "Sur l'écran où se trouve la souris",
        "Na tela onde estiver o mouse");
    public static string ScreenLabel(int number, bool isPrimary, int width, int height)
    {
        var primary = isPrimary ? T(" · Primary", " · Principal",
            " · Primär", " · Principal", " · Principal") : "";
        return T($"Screen {number}{primary} ({width}×{height})", $"Pantalla {number}{primary} ({width}×{height})",
            $"Bildschirm {number}{primary} ({width}×{height})",
            $"Écran {number}{primary} ({width}×{height})",
            $"Tela {number}{primary} ({width}×{height})");
    }

    public static string HideOnFullscreenCheckbox => T("Hide dock in fullscreen", "Ocultar dock a pantalla completa",
        "Dock im Vollbildmodus ausblenden", "Masquer le dock en plein écran", "Ocultar dock em tela cheia");
    public static string HideOnFullscreenHint => T("Hides the dock automatically over any real fullscreen window — games, videos, presentations. Doesn't affect regular maximized windows (like a browser with tabs).", "Oculta el dock automáticamente ante cualquier ventana a pantalla completa de verdad — juegos, vídeos, presentaciones. No afecta a ventanas normales maximizadas (como el navegador con pestañas).",
        "Blendet das Dock automatisch über jedes echte Vollbildfenster aus — Spiele, Videos, Präsentationen. Beeinflusst keine normalen maximierten Fenster (wie ein Browser mit Tabs).",
        "Masque le dock automatiquement devant toute fenêtre en plein écran réel — jeux, vidéos, présentations. N'affecte pas les fenêtres normales agrandies (comme un navigateur avec des onglets).",
        "Oculta o dock automaticamente sobre qualquer janela em tela cheia real — jogos, vídeos, apresentações. Não afeta janelas normais maximizadas (como um navegador com abas).");
    public static string ShowNotePreviewCheckbox => T("Show a preview of the text", "Mostrar una vista previa del texto",
        "Textvorschau anzeigen", "Afficher un aperçu du texte", "Mostrar uma prévia do texto");
    public static string ShowNotePreviewHint => T("A second line with the start of each note, in the dock tabs and in Manage notes. Turned off, only titles are shown.", "Una segunda línea con el principio de cada nota, en las pestañas del dock y en Gestionar notas. Si la desactivas, solo se ven los títulos.",
        "Eine zweite Zeile mit dem Anfang jeder Notiz, in den Dock-Tabs und in Notizen verwalten. Deaktiviert werden nur Titel angezeigt.",
        "Une deuxième ligne avec le début de chaque note, dans les onglets du dock et dans Gérer les notes. Désactivé, seuls les titres sont affichés.",
        "Uma segunda linha com o início de cada nota, nas abas do dock e em Gerenciar notas. Desativado, apenas os títulos são exibidos.");
    public static string KeepDockOpenCheckbox => T("Keep dock open", "Mantener el dock abierto",
        "Dock offen halten", "Garder le dock ouvert", "Manter o dock aberto");
    public static string KeepDockOpenHint => T("Keeps the dock expanded until you turn this off.", "Mantiene el dock desplegado hasta que desactives esta opción.",
        "Hält das Dock ausgeklappt, bis du diese Option deaktivierst.",
        "Garde le dock déployé jusqu'à ce que vous désactiviez cette option.",
        "Mantém o dock expandido até que você desative esta opção.");

    public static string EdgeSectionTitle => T("Screen edge", "Lado de la pantalla",
        "Bildschirmseite", "Bord de l'écran", "Lado da tela");
    public static string EdgeSectionHint => T("Which edge the Aldune dock lives on.", "En qué borde vive el dock de Aldune.",
        "An welchem Rand das Aldune-Dock positioniert ist.",
        "Sur quel bord se trouve le dock Aldune.",
        "Em qual borda o dock do Aldune fica posicionado.");
    public static string EdgeRight => T("Right", "Derecha",
        "Rechts", "Droite", "Direita");
    public static string EdgeLeft => T("Left", "Izquierda",
        "Links", "Gauche", "Esquerda");
    public static string EdgeTop => T("Top", "Arriba",
        "Oben", "Haut", "Cima");
    public static string EdgeBottom => T("Bottom", "Abajo",
        "Unten", "Bas", "Baixo");

    public static string RememberPositionsCheckbox => T("Remember note positions", "Recordar la posición de las notas",
        "Notizpositionen merken", "Mémoriser les positions des notes", "Lembrar a posição das notas");
    public static string RememberPositionsHint => T("When you close a note, it reopens in the same spot and size on the desktop — like a real sticky note.", "Al cerrar una nota, la próxima vez se abre en el mismo sitio y con el mismo tamaño en el escritorio — como un post-it de verdad.",
        "Wenn du eine Notiz schließt, öffnet sie sich beim nächsten Mal an derselben Stelle und in derselben Größe auf dem Desktop — wie ein echtes Haftnotizzettel.",
        "Lorsque vous fermez une note, elle se rouvre au même endroit et dans la même taille sur le bureau — comme un véritable Post-it.",
        "Ao fechar uma nota, ela reabre no mesmo lugar e tamanho na área de trabalho — como um post-it de verdade.");

    public static string MoveCompletedTasksCheckbox => T("Move checked tasks to the end of the list", "Mover las tareas hechas al final de la lista",
        "Erledigte Aufgaben ans Ende der Liste verschieben",
        "Déplacer les tâches cochées en bas de la liste",
        "Mover tarefas concluídas para o final da lista");
    public static string MoveCompletedTasksHint => T("What's left stays together at the top, like a shopping list. Unchecking one moves it back up.", "Lo pendiente queda junto arriba, como en una lista de la compra. Al desmarcar una, vuelve a subir.",
        "Das Ausstehende bleibt zusammen oben, wie bei einer Einkaufsliste. Wenn du eine Aufgabe abbrichst, kommt sie wieder nach oben.",
        "Ce qui reste à faire reste groupé en haut, comme une liste de courses. Décocher une tâche la fait remonter.",
        "O que falta fica agrupado no topo, como uma lista de compras. Desmarcar uma tarefa a move de volta para cima.");
    public static string AutoHideCompletedTasksCheckbox => T("Delete completed tasks automatically", "Borrar automáticamente las tareas completadas",
        "Erledigte Aufgaben automatisch löschen",
        "Supprimer automatiquement les tâches terminées",
        "Excluir tarefas concluídas automaticamente");
    public static string AutoHideCompletedTasksHint => T("A checked task stays for a while so you can still see it, then its line is removed from the note for good.", "Una tarea marcada se queda un tiempo por si quieres verla, y después su línea se borra de la nota para siempre.",
        "Eine erledigte Aufgabe bleibt eine Weile sichtbar, dann wird ihre Zeile endgültig aus der Notiz entfernt.",
        "Une tâche cochée reste visible un moment pour que vous puissiez encore la voir, puis sa ligne est supprimée définitivement de la note.",
        "Uma tarefa marcada fica visível por um tempo para que você ainda possa vê-la, e depois sua linha é removida permanentemente da nota.");
    public static string AutoHideCompletedTasksDelayLabel => T("After:", "Después de:",
        "Danach:", "Après :", "Depois de:");
    public static string TaskDelayMinutesUnit => T("minutes", "minutos",
        "Minuten", "minutes", "minutos");
    public static string TaskDelayHoursUnit => T("hours", "horas",
        "Stunden", "heures", "horas");
    public static string TaskDelayDaysUnit => T("days", "días",
        "Tage", "jours", "dias");
    public static string TaskDelayWeeksUnit => T("weeks", "semanas",
        "Wochen", "semaines", "semanas");

    public static string TrashRetentionTitle => T("Trash", "Papelera",
        "Papierkorb", "Corbeille", "Lixeira");
    public static string TrashRetentionHint => T("A trashed note is deleted for good after this many days.", "Una nota en la papelera se borra para siempre pasados estos días.",
        "Eine Notiz im Papierkorb wird nach dieser Anzahl von Tagen endgültig gelöscht.",
        "Une note dans la corbeille est supprimée définitivement après ce nombre de jours.",
        "Uma nota na lixeira é excluída permanentemente após esses dias.");
    public static string TrashRetentionDaysUnit => T("days", "días",
        "Tage", "jours", "dias");

    public static string SyncSectionTitle => T("Sync between devices", "Sincronización entre dispositivos",
        "Geräteübergreifende Synchronisierung",
        "Synchronisation entre appareils",
        "Sincronização entre dispositivos");
    public static string SyncSectionHint => T(
        "Notes stay encrypted. Use a shared folder, your own Aldune server, or WebDAV.",
        "Las notas permanecen cifradas. Usa una carpeta compartida o tu propio servidor de sincronización de Aldune.",
        "Notizen bleiben verschlüsselt. Verwende einen freigegebenen Ordner, deinen eigenen Aldune-Server oder WebDAV.",
        "Les notes restent chiffrées. Utilisez un dossier partagé, votre propre serveur Aldune ou WebDAV.",
        "As notas permanecem criptografadas. Use uma pasta compartilhada, seu próprio servidor Aldune ou WebDAV.");
    public static string SyncEnabledCheckbox => T("Enable sync", "Activar sincronización",
        "Synchronisierung aktivieren", "Activer la synchronisation", "Ativar sincronização");
    public static string SyncFolderOption => T("Shared folder / NAS", "Carpeta compartida / NAS",
        "Freigegebener Ordner / NAS", "Dossier partagé / NAS", "Pasta compartilhada / NAS");
    public static string SyncServerOption => T("Self-hosted server", "Servidor propio",
        "Eigener Server", "Serveur auto-hébergé", "Servidor próprio");
    public static string SyncWebDavOption => T("WebDAV / Nextcloud", "WebDAV / Nextcloud",
        "WebDAV / Nextcloud", "WebDAV / Nextcloud", "WebDAV / Nextcloud");
    public static string SyncFolderLabel => T("Folder:", "Carpeta:",
        "Ordner:", "Dossier :", "Pasta:");
    public static string SyncServerUrlLabel => T("Server URL:", "URL del servidor:",
        "Server-URL:", "URL du serveur :", "URL do servidor:");
    public static string SyncServerTokenLabel => T("Access token:", "Token de acceso:",
        "Zugriffstoken:", "Jeton d'accès :", "Token de acesso:");
    public static string SyncWebDavUsernameLabel => T("Username:", "Usuario:",
        "Benutzername:", "Nom d'utilisateur :", "Usuário:");
    public static string SyncWebDavPasswordLabel => T("Password:", "Contraseña:",
        "Passwort:", "Mot de passe :", "Senha:");
    public static string SyncWebDavHint => T(
        "Use an app password when your provider supports it. Aldune stores it protected on this device.",
        "Usa una contraseña de aplicación si tu proveedor la admite. Aldune la protege en este dispositivo.",
        "Verwende ein App-Passwort, wenn dein Anbieter es unterstützt. Aldune speichert es geschützt auf diesem Gerät.",
        "Utilisez un mot de passe d'application si votre fournisseur le permet. Aldune le stocke de façon sécurisée sur cet appareil.",
        "Use uma senha de aplicativo se seu provedor suportar. Aldune a armazena protegida neste dispositivo.");
    public static string SyncBrowseButton => T("Browse…", "Examinar…",
        "Durchsuchen…", "Parcourir…", "Procurar…");
    public static string SyncCodeLabel => T("Device sync code:", "Código de sincronización:",
        "Geräte-Sync-Code:", "Code de synchronisation :", "Código de sincronização:");
    public static string SyncGenerateCodeButton => T("Generate / copy", "Generar / copiar",
        "Generieren / kopieren", "Générer / copier", "Gerar / copiar");
    public static string SyncShareProfileButton => T("Share profile", "Compartir perfil",
        "Profil teilen", "Partager le profil", "Compartilhar perfil");
    public static string SyncRevokeAccessButton => T("Revoke old codes", "Revocar códigos anteriores",
        "Alte Codes widerrufen", "Révoquer les anciens codes", "Revogar códigos antigos");
    public static string SyncRevokeAccessHint => T(
        "Replaces this link's key. Devices using an old code will need a new invitation.",
        "Cambia la clave de este vínculo. Los dispositivos con un código antiguo necesitarán una invitación nueva.",
        "Ersetzt den Schlüssel dieses Links. Geräte mit einem alten Code benötigen eine neue Einladung.",
        "Remplace la clé de ce lien. Les appareils utilisant un ancien code auront besoin d'une nouvelle invitation.",
        "Substitui a chave deste link. Dispositivos com um código antigo precisarão de um novo convite.");
    public static string SyncRevokeAccessConfirm => T(
        "Replace this profile's sync key? Every device using an older code will stop syncing until it imports a new invitation.",
        "¿Cambiar la clave de este perfil? Los dispositivos que usen un código antiguo dejarán de sincronizarse hasta importar una invitación nueva.",
        "Den Sync-Schlüssel dieses Profils ersetzen? Alle Geräte mit einem alten Code werden nicht mehr synchronisiert, bis sie eine neue Einladung importieren.",
        "Remplacer la clé de synchronisation de ce profil ? Tout appareil utilisant un ancien code arrêterait de se synchroniser jusqu'à importer une nouvelle invitation.",
        "Substituir a chave de sincronização deste perfil? Todos os dispositivos com um código antigo pararão de sincronizar até importar um novo convite.");
    public static string SyncRevokeAccessCompletedStatus => T(
        "Old profile codes were revoked. Generate and share a new invitation with the devices that should keep syncing.",
        "Los códigos antiguos del perfil han sido revocados. Genera y comparte una invitación nueva con los dispositivos autorizados.",
        "Alte Profilcodes wurden widerrufen. Generiere und teile eine neue Einladung mit den Geräten, die weiterhin synchronisieren sollen.",
        "Les anciens codes du profil ont été révoqués. Générez et partagez une nouvelle invitation avec les appareils autorisés.",
        "Os códigos antigos do perfil foram revogados. Gere e compartilhe um novo convite com os dispositivos que devem continuar sincronizando.");
    public static string SyncShareCodeHint => T(
        "This invitation also carries the profile name and self-hosted server URL. It never contains the access token.",
        "Esta invitación también lleva el nombre del perfil y la URL del servidor propio. Nunca contiene el token de acceso.",
        "Diese Einladung enthält auch den Profilnamen und die URL des eigenen Servers. Sie enthält niemals das Zugriffstoken.",
        "Cette invitation contient aussi le nom du profil et l'URL du serveur auto-hébergé. Elle ne contient jamais le jeton d'accès.",
        "Este convite também contém o nome do perfil e a URL do servidor próprio. Nunca contém o token de acesso.");
    public static string SyncShareCodeCopiedStatus => T("Profile code copied to the clipboard.", "Código de perfil copiado al portapapeles.",
        "Profilcode in die Zwischenablage kopiert.",
        "Code du profil copié dans le presse-papiers.",
        "Código do perfil copiado para a área de transferência.");
    public static string SyncImportCodeButton => T("Import code", "Importar código",
        "Code importieren", "Importer le code", "Importar código");
    public static string SyncNowButton => T("Sync now", "Sincronizar ahora",
        "Jetzt synchronisieren", "Synchroniser maintenant", "Sincronizar agora");
    public static string SyncInProgressStatus => T("Syncing…", "Sincronizando…",
        "Synchronisiere…", "Synchronisation en cours…", "Sincronizando…");
    public static string SyncProfileLabel => T("Sync profile", "Perfil de sincronización",
        "Synchronisierungsprofil", "Profil de synchronisation", "Perfil de sincronização");
    public static string SyncProfileNameLabel => T("Name:", "Nombre:",
        "Name:", "Nom :", "Nome:");
    public static string SyncNewProfile => T("New", "Nuevo",
        "Neu", "Nouveau", "Novo");
    public static string SyncDeleteProfile => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir");
    public static string SyncProfileNewName(int number) => T($"Sync link {number}", $"Vínculo {number}",
        $"Sync-Link {number}", $"Lien de synchronisation {number}", $"Link de sincronização {number}");
    public static string SyncProfileDeleteConfirm => T(
        "Delete this sync profile? Its local connection settings will be removed.",
        "¿Eliminar este perfil de sincronización? Se borrarán sus ajustes de conexión locales.",
        "Dieses Synchronisierungsprofil löschen? Die lokalen Verbindungseinstellungen werden entfernt.",
        "Supprimer ce profil de synchronisation ? Les paramètres de connexion locaux seront supprimés.",
        "Excluir este perfil de sincronização? As configurações de conexão locais serão removidas.");
    public static string SyncProfileLastRemaining => T(
        "Keep at least one sync profile.",
        "Debe quedar al menos un perfil de sincronización.",
        "Mindestens ein Synchronisierungsprofil muss vorhanden bleiben.",
        "Conservez au moins un profil de synchronisation.",
        "Mantenha pelo menos um perfil de sincronização.");
    public static string SyncScopeAll => T("All notes", "Todas las notas",
        "Alle Notizen", "Toutes les notes", "Todas as notas");
    public static string SyncScopeSelected => T("Selected notes", "Notas seleccionadas",
        "Ausgewählte Notizen", "Notes sélectionnées", "Notas selecionadas");
    public static string SyncScopeTag => T("Notes with a tag", "Notas con una etiqueta",
        "Notizen mit einem Tag", "Notes avec une étiquette", "Notas com uma etiqueta");
    public static string SyncScopeHint => T(
        "Choose which notes this sync link can exchange. The selection stays local to this device.",
        "Elige qué notas puede intercambiar este vínculo. La selección se guarda solo en este dispositivo.",
        "Wähle aus, welche Notizen dieser Synchronisierungslink austauschen darf. Die Auswahl bleibt lokal auf diesem Gerät.",
        "Choisissez les notes que ce lien de synchronisation peut échanger. La sélection reste locale sur cet appareil.",
        "Escolha quais notas este link de sincronização pode trocar. A seleção fica salva apenas neste dispositivo.");
    public static string SyncChooseNotes => T("Choose notes…", "Elegir notas…",
        "Notizen auswählen…", "Choisir des notes…", "Escolher notas…");
    public static string SyncChooseTag => T("Choose tag…", "Elegir etiqueta…",
        "Tag auswählen…", "Choisir une étiquette…", "Escolher etiqueta…");
    public static string SyncSelectedCount(int count) => T(
        $"{count} selected for sync", $"{count} seleccionadas para sincronizar",
        $"{count} für die Synchronisierung ausgewählt",
        $"{count} sélectionnée(s) pour la synchronisation",
        $"{count} selecionada(s) para sincronizar");
    public static string SyncNotesWindowTitle => T("Choose notes to sync", "Elegir notas para sincronizar",
        "Notizen zum Synchronisieren auswählen",
        "Choisir les notes à synchroniser",
        "Escolher notas para sincronizar");
    public static string SyncNotesWindowHint => T(
        "Only the checked notes and their changes will use this sync link.",
        "Solo las notas marcadas y sus cambios usarán este vínculo de sincronización.",
        "Nur die markierten Notizen und ihre Änderungen verwenden diesen Synchronisierungslink.",
        "Seules les notes cochées et leurs modifications utiliseront ce lien de synchronisation.",
        "Somente as notas marcadas e suas alterações usarão este link de sincronização.");
    public static string SyncNotesSave => T("Save selection", "Guardar selección",
        "Auswahl speichern", "Enregistrer la sélection", "Salvar seleção");
    public static string SyncNotesCancel => T("Cancel", "Cancelar",
        "Abbrechen", "Annuler", "Cancelar");
    public static string SyncNotesEmpty => T("There are no notes to choose yet.", "Todavía no hay notas para elegir.",
        "Es gibt noch keine Notizen zur Auswahl.",
        "Il n'y a pas encore de notes à choisir.",
        "Ainda não há notas para escolher.");
    public static string SyncAutomaticCheckbox => T("Sync periodically", "Sincronizar periódicamente",
        "Regelmäßig synchronisieren", "Synchroniser périodiquement", "Sincronizar periodicamente");
    public static string SyncAutomaticHint => T("Aldune checks for changes in the background while it is running.", "Aldune comprueba cambios en segundo plano mientras está abierta.",
        "Aldune prüft im Hintergrund auf Änderungen, während es läuft.",
        "Aldune vérifie les modifications en arrière-plan pendant son exécution.",
        "Aldune verifica alterações em segundo plano enquanto está aberto.");
    public static string SyncIntervalLabel => T("Every minutes:", "Cada minutos:",
        "Alle Minuten:", "Toutes les minutes :", "A cada minutos:");
    public static string SyncCodeHint => T(
        "Use the same code on each device. Keep it private: it unlocks your notes.",
        "Usa el mismo código en cada dispositivo. Guárdalo en privado: permite descifrar tus notas.",
        "Verwende auf jedem Gerät denselben Code. Halte ihn privat: Er entsperrt deine Notizen.",
        "Utilisez le même code sur chaque appareil. Gardez-le privé : il déverrouille vos notes.",
        "Use o mesmo código em cada dispositivo. Mantenha-o privado: ele desbloqueia suas notas.");
    public static string SyncDisabledStatus => T("Sync is disabled.", "La sincronización está desactivada.",
        "Die Synchronisierung ist deaktiviert.",
        "La synchronisation est désactivée.",
        "A sincronização está desativada.");
    public static string SyncReadyStatus => T("Ready to sync.", "Lista para sincronizar.",
        "Bereit zur Synchronisierung.", "Prêt à synchroniser.", "Pronto para sincronizar.");
    public static string SyncCompletedStatus(int uploaded, int downloaded) =>
        T($"Sync complete: {uploaded} uploaded, {downloaded} downloaded.",
          $"Sincronización completada: {uploaded} subidas, {downloaded} descargadas.",
            $"Synchronisierung abgeschlossen: {uploaded} hochgeladen, {downloaded} heruntergeladen.",
            $"Synchronisation terminée : {uploaded} envoyée(s), {downloaded} reçue(s).",
            $"Sincronização concluída: {uploaded} enviada(s), {downloaded} baixada(s).");
    public static string SyncErrorStatus(string details) => T($"Sync error: {details}", $"Error de sincronización: {details}",
        $"Synchronisierungsfehler: {details}",
        $"Erreur de synchronisation : {details}",
        $"Erro de sincronização: {details}");
    public static string SyncCodeCopiedStatus => T("Code copied to the clipboard.", "Código copiado al portapapeles.",
        "Code in die Zwischenablage kopiert.",
        "Code copié dans le presse-papiers.",
        "Código copiado para a área de transferência.");
    public static string SyncCodeImportedStatus => T(
        "Code imported. Check the connection and add the access token if this is a self-hosted server.",
        "Código importado. Comprueba la conexión y añade el token de acceso si es un servidor propio.",
        "Code importiert. Prüfe die Verbindung und füge das Zugriffstoken hinzu, falls es ein eigener Server ist.",
        "Code importé. Vérifiez la connexion et ajoutez le jeton d'accès si c'est un serveur auto-hébergé.",
        "Código importado. Verifique a conexão e adicione o token de acesso se for um servidor próprio.");
    public static string SyncInvalidCode => T("That sync code is not valid.", "Ese código de sincronización no es válido.",
        "Dieser Synchronisierungscode ist nicht gültig.",
        "Ce code de synchronisation n'est pas valide.",
        "Esse código de sincronização não é válido.");
    public static string SyncLastSyncNever => T("Last sync: never", "Última sincronización: nunca",
        "Letzte Synchronisierung: nie", "Dernière synchronisation : jamais", "Última sincronização: nunca");
    public static string SyncLastSyncAt(DateTimeOffset at) =>
        T($"Last sync: {at.ToLocalTime():g}", $"Última sincronización: {at.ToLocalTime():g}",
            $"Letzte Synchronisierung: {at.ToLocalTime():g}",
            $"Dernière synchronisation : {at.ToLocalTime():g}",
            $"Última sincronização: {at.ToLocalTime():g}");
    public static string SyncConflictsButton => T("Review conflicts", "Revisar conflictos",
        "Konflikte prüfen", "Vérifier les conflits", "Revisar conflitos");
    public static string SyncConflictsCount(int count) =>
        T($"{count} conflict(s) saved for review", $"{count} conflicto(s) guardado(s) para revisar",
            $"{count} Konflikt(e) zur Überprüfung gespeichert",
            $"{count} conflit(s) enregistré(s) à vérifier",
            $"{count} conflito(s) salvo(s) para revisar");
    public static string SyncNoConflicts => T("No saved conflicts.", "No hay conflictos guardados.",
        "Keine gespeicherten Konflikte.", "Aucun conflit enregistré.", "Nenhum conflito salvo.");
    public static string SyncConflictTitle => T("Sync conflicts", "Conflictos de sincronización",
        "Synchronisierungskonflikte", "Conflits de synchronisation", "Conflitos de sincronização");
    public static string SyncConflictHint => T(
        "Aldune kept the winning version active. You can restore the other version or dismiss this record.",
        "Aldune mantiene activa la versión ganadora. Puedes restaurar la otra versión o descartar este registro.",
        "Aldune hat die gewinnende Version aktiv gehalten. Du kannst die andere Version wiederherstellen oder diesen Eintrag verwerfen.",
        "Aldune a conservé la version gagnante active. Vous pouvez restaurer l'autre version ou ignorer cet enregistrement.",
        "Aldune manteve a versão vencedora ativa. Você pode restaurar a outra versão ou descartar este registro.");
    public static string SyncConflictRestore => T("Restore this version", "Restaurar esta versión",
        "Diese Version wiederherstellen", "Restaurer cette version", "Restaurar esta versão");
    public static string SyncConflictDismiss => T("Dismiss", "Descartar",
        "Verwerfen", "Ignorer", "Descartar");
    public static string SyncConflictDismissAll => T("Dismiss all", "Descartar todo",
        "Alle verwerfen", "Tout ignorer", "Descartar tudo");
    public static string SyncConflictDismissAllConfirm(int count) => T(
        $"Dismiss all {count} conflicts? The losing versions will be discarded permanently.",
        $"¿Descartar los {count} conflictos? Las versiones perdedoras se descartarán definitivamente.",
        $"Alle {count} Konflikte verwerfen? Die unterlegenen Versionen werden dauerhaft gelöscht.",
        $"Ignorer les {count} conflits ? Les versions perdantes seront définitivement supprimées.",
        $"Descartar os {count} conflitos? As versões perdedoras serão descartadas definitivamente.");
    public static string SyncConflictDeleted => T("Deleted version", "Versión eliminada",
        "Gelöschte Version", "Version supprimée", "Versão excluída");
    public static string SyncErrorTitle => T("Aldune sync", "Sincronización de Aldune",
        "Aldune-Synchronisierung", "Synchronisation Aldune", "Sincronização do Aldune");
    public static string SyncUnauthorizedStatus => T(
        "The server rejected the token. Paste only the value after ALDUNE_SYNC_TOKEN=.",
        "El servidor ha rechazado el token. Pega solo el valor que aparece después de ALDUNE_SYNC_TOKEN=.",
        "Der Server hat das Token abgelehnt. Füge nur den Wert nach ALDUNE_SYNC_TOKEN= ein.",
        "Le serveur a rejeté le jeton. Collez uniquement la valeur après ALDUNE_SYNC_TOKEN=.",
        "O servidor rejeitou o token. Cole apenas o valor após ALDUNE_SYNC_TOKEN=.");

    public static string LanguageSectionTitle => T("Language", "Idioma",
        "Sprache", "Langue", "Idioma");
    public static string AppearanceSectionTitle => T("Appearance", "Aspecto",
        "Erscheinungsbild", "Apparence", "Aparência");
    public static string AppearanceSectionHint => T("Windows, the dock and messages. Notes keep their own colors.", "Ventanas, dock y avisos. Las notas conservan sus colores.",
        "Fenster, Dock und Meldungen. Notizen behalten ihre eigenen Farben.",
        "Fenêtres, dock et messages. Les notes conservent leurs propres couleurs.",
        "Janelas, dock e avisos. As notas mantêm suas próprias cores.");
    public static string AppearanceDark => T("Dark", "Oscuro",
        "Dunkel", "Sombre", "Escuro");
    public static string AppearanceLight => T("Light", "Claro",
        "Hell", "Clair", "Claro");
    public static string AppearancePastel => T("Pastel", "Pastel", "Pastell", "Pastel", "Pastel");
    public static string AppearanceMidnight => T("Midnight", "Medianoche", "Mitternacht", "Minuit", "Meia-noite");
    public static string AppearanceXpLight => T("XP Light", "XP claro", "XP hell", "XP clair", "XP claro");
    public static string AppearanceXpDark => T("XP + 95 Dark", "XP + 95 oscuro", "XP + 95 dunkel", "XP + 95 sombre", "XP + 95 escuro");
    public static string AppearanceTelecomLight => T("Telecom Light (lab)", "Telecomunicaciones claro (laboratorio)",
        "Telekom hell (Labor)", "Télécom clair (labo)", "Telecom claro (laboratório)");
    public static string AppearanceTelecomDark => T("Telecom Dark (oscilloscope)", "Telecomunicaciones oscuro (osciloscopio)",
        "Telekom dunkel (Oszilloskop)", "Télécom sombre (oscilloscope)", "Telecom escuro (osciloscópio)");
    public static string AppearanceBash => T("Bash", "Bash", "Bash", "Bash", "Bash");
    public static string AppearancePhosphor => T("Phosphor (old monitor)", "Fósforo (monitor antiguo)",
        "Phosphor (alter Monitor)", "Phosphore (vieux moniteur)", "Fósforo (monitor antigo)");

    /// <summary>Nombre visible de cada hueco de color de un aspecto (el id viene de AspectCatalog).</summary>
    public static string AspectSlotName(string slot) => slot switch
    {
        "titleBar" => T("Title bar", "Barra de título", "Titelleiste", "Barre de titre", "Barra de título"),
        "accent" => T("Accent", "Acento", "Akzent", "Accent", "Destaque"),
        "ink" => T("Ink", "Tinta", "Tinte", "Encre", "Tinta"),
        "panel" => T("Panel", "Panel", "Bedienfeld", "Panneau", "Painel"),
        "trace" => T("Trace (CH1)", "Traza (CH1)", "Spur (CH1)", "Trace (CH1)", "Traço (CH1)"),
        "background" => T("Terminal", "Terminal", "Terminal", "Terminal", "Terminal"),
        "user" => T("User", "Usuario", "Benutzer", "Utilisateur", "Usuário"),
        "path" => T("Path", "Ruta", "Pfad", "Chemin", "Caminho"),
        "phosphor" => T("Phosphor", "Fósforo", "Phosphor", "Phosphore", "Fósforo"),
        "details" => T("Details", "Detalles", "Details", "Détails", "Detalhes"),
        _ => slot,
    };

    public static string AspectPalettePresets => T("Palette", "Paleta", "Palette", "Palette", "Paleta");
    public static string AspectResetColors => T("Reset colors", "Restablecer colores", "Farben zurücksetzen",
        "Réinitialiser les couleurs", "Redefinir cores");
    public static string AspectSuggestTheme(string theme) => T(
        $"Also use the “{theme}” note theme?",
        $"¿Usar también el tema de notas «{theme}»?",
        $"Auch das Notizthema „{theme}“ verwenden?",
        $"Utiliser aussi le thème de notes « {theme} » ?",
        $"Usar também o tema de notas “{theme}”?");
    public static string AppearanceSystem => T("Same as Windows", "Como Windows",
        "Wie Windows", "Comme Windows", "Como Windows");
    public static string AppearanceUniformColor => T("Same color for every note", "Mismo color en todas las notas",
        "Gleiche Farbe für alle Notizen", "Même couleur pour toutes les notes", "Mesma cor em todas as notas");
    public static string AppearanceUniformColorHint => T("Only changes how they look: each note keeps its own color.",
        "Solo cambia cómo se ven: cada nota conserva su color.",
        "Ändert nur das Aussehen: Jede Notiz behält ihre eigene Farbe.",
        "Ne change que l'apparence : chaque note garde sa couleur.",
        "Só muda a aparência: cada nota mantém a sua cor.");
    public static string AppearanceSyncSignal => T("Show sync status on each note", "Señal de sincronización en cada nota",
        "Synchronisierungsstatus auf jeder Notiz", "Indicateur de synchronisation sur chaque note", "Sinal de sincronização em cada nota");
    public static string AppearanceSquareCorners => T("Square corners on notes, dock and windows", "Esquinas rectas en notas, dock y ventanas",
        "Eckige Ecken bei Notizen, Dock und Fenstern", "Coins carrés pour les notes, le dock et les fenêtres", "Cantos retos nas notas, no dock e nas janelas");
    public static string SyncSignalLabel(Aldune.Core.SyncSignalState state) => state switch
    {
        Aldune.Core.SyncSignalState.Synced => T("synced", "sincronizada", "synchronisiert", "synchronisée", "sincronizada"),
        Aldune.Core.SyncSignalState.Pending => T("pending", "pendiente", "ausstehend", "en attente", "pendente"),
        Aldune.Core.SyncSignalState.Conflict => T("conflict", "conflicto", "Konflikt", "conflit", "conflito"),
        Aldune.Core.SyncSignalState.Excluded => T("not synced", "no se sincroniza", "wird nicht synchronisiert", "non synchronisée", "não sincronizada"),
        _ => "",
    };
    public static string AppearanceUniformColorPick => T("Choose color…", "Elegir color…",
        "Farbe wählen…", "Choisir la couleur…", "Escolher cor…");
    public static string LanguageNameIn(string code) => code switch
    {
        "de" => T("German", "Alemán", "Deutsch", "Allemand", "Alemão"),
        "en" => T("English", "Inglés", "Englisch", "Anglais", "Inglês"),
        "es" => T("Spanish", "Español", "Spanisch", "Espagnol", "Espanhol"),
        "fr" => T("French", "Francés", "Französisch", "Français", "Francês"),
        "pt" => T("Portuguese (Brazil)", "Portugués (Brasil)", "Portugiesisch (Brasilien)", "Portugais (Brésil)", "Português (Brasil)"),
        _ => code
    };
    public static string LanguageSectionHint => T("Restarting Aldune applies the change to every window.", "Reiniciar Aldune aplica el cambio en todas las ventanas.",
        "Aldune neu starten, um die Änderung in allen Fenstern zu übernehmen.",
        "Redémarrer Aldune applique la modification à toutes les fenêtres.",
        "Reiniciar o Aldune aplica a mudança em todas as janelas.");

    // --- Temas -----------------------------------------------------------------------------------

    public static string ThemesSectionTitle => T("Note colors", "Colores de las notas",
        "Notizfarben", "Couleurs des notes", "Cores das notas");
    public static string ThemesSectionHint => T(
        "The theme sets the colors for new notes and the quick colors in each note's menu.",
        "El tema decide el color de las notas nuevas y los colores rápidos del menú de cada nota.",
        "Das Thema legt die Farben für neue Notizen und die Schnellfarben im Menü jeder Notiz fest.",
        "Le thème définit les couleurs des nouvelles notes et les couleurs rapides dans le menu de chaque note.",
        "O tema define as cores para novas notas e as cores rápidas no menu de cada nota.");
    public static string ThemeNew => T("New", "Nuevo",
        "Neu", "Nouveau", "Novo");
    public static string ThemeDuplicate => T("Duplicate", "Duplicar",
        "Duplizieren", "Dupliquer", "Duplicar");
    public static string ThemeEdit => T("Edit", "Editar",
        "Bearbeiten", "Modifier", "Editar");
    public static string ThemeDeleteButton => T("Delete", "Eliminar",
        "Löschen", "Supprimer", "Excluir");
    public static string ThemeDeleteConfirm(string name) => T(
        $"Delete the theme \"{name}\"? Your notes keep their colors.",
        $"¿Eliminar el tema «{name}»? Tus notas conservan sus colores.",
        $"Das Thema \"{name}\" löschen? Deine Notizen behalten ihre Farben.",
        $"Supprimer le thème \"{name}\" ? Vos notes conservent leurs couleurs.",
        $"Excluir o tema \"{name}\"? Suas notas mantêm suas cores.");
    public static string ThemeCopyName(string name) => T($"{name} (copy)", $"{name} (copia)",
        $"{name} (Kopie)", $"{name} (copie)", $"{name} (cópia)");
    public static string ThemeNewName => T("My theme", "Mi tema",
        "Mein Thema", "Mon thème", "Meu tema");
    public static string NewNoteToneLabel => T("New notes are", "Las notas nuevas son",
        "Neue Notizen sind", "Les nouvelles notes sont", "As novas notas são");
    public static string ToneLight => T("Light", "Claras",
        "Hell", "Claires", "Claras");
    public static string ToneDark => T("Dark", "Oscuras",
        "Dunkel", "Sombres", "Escuras");
    public static string ToneBoth => T("Both, alternating", "De los dos tipos, alternando",
        "Beide, abwechselnd", "Les deux, en alternance", "As duas, alternando");
    public static string ColorAssignmentLabel => T("Color of new notes", "Color de las notas nuevas",
        "Farbe neuer Notizen", "Couleur des nouvelles notes", "Cor das novas notas");
    public static string AssignAvoidNeighbors => T(
        "Rotate, never the same as the one next to it (recommended)",
        "Rotar, sin repetir el de al lado (recomendado)",
        "Rotieren, nie dieselbe wie die daneben (empfohlen)",
        "En rotation, jamais la même que la voisine (recommandé)",
        "Rodar, nunca a mesma que a do lado (recomendado)");
    public static string AssignRotate => T("Rotate through the theme", "Rotar por el tema",
        "Durch das Thema rotieren", "Parcourir le thème en rotation", "Rodar pelo tema");
    public static string AssignMostDistinct => T("The most different from the rest", "El más distinto del resto",
        "Die am meisten von den anderen abweichende",
        "La plus différente des autres",
        "A mais diferente das demais");
    public static string AssignFixed => T("Always the same color", "Siempre el mismo color",
        "Immer dieselbe Farbe", "Toujours la même couleur", "Sempre a mesma cor");
    public static string ApplyThemeButton => T("Apply to existing notes…", "Aplicar a las notas existentes…",
        "Auf vorhandene Notizen anwenden…",
        "Appliquer aux notes existantes…",
        "Aplicar às notas existentes…");
    public static string ApplyThemeConfirmTitle => T("Apply theme", "Aplicar tema",
        "Thema anwenden", "Appliquer le thème", "Aplicar tema");
    public static string ApplyThemeConfirm(int count) => count == 1
        ? T("The color of your active note will change, even if you picked it by hand. It will sync to your other devices.",
            "Se cambiará el color de tu nota activa, aunque lo eligieras a mano. Se sincronizará a tus otros dispositivos.",
            "Die Farbe deiner aktiven Notiz wird geändert, auch wenn du sie selbst ausgewählt hast. Sie wird auf deine anderen Geräte synchronisiert.",
            "La couleur de votre note active sera modifiée, même si vous l'avez choisie manuellement. Elle sera synchronisée sur vos autres appareils.",
            "A cor da sua nota ativa será alterada, mesmo que você a tenha escolhido manualmente. Ela será sincronizada com seus outros dispositivos.")
        : T($"The color of your {count} active notes will change, including colors you picked by hand. It will sync to your other devices.",
            $"Se cambiará el color de tus {count} notas activas, incluidos los que elegiste a mano. Se sincronizará a tus otros dispositivos.",
            $"Die Farbe deiner {count} aktiven Notizen wird geändert, einschließlich der von dir selbst ausgewählten. Sie werden auf deine anderen Geräte synchronisiert.",
            $"La couleur de vos {count} notes actives sera modifiée, y compris celles que vous avez choisies manuellement. Elles seront synchronisées sur vos autres appareils.",
            $"A cor das suas {count} notas ativas será alterada, incluindo as que você escolheu manualmente. Elas serão sincronizadas com seus outros dispositivos.");

    public static string ThemeEditorTitle => T("Edit theme", "Editar tema",
        "Thema bearbeiten", "Modifier le thème", "Editar tema");
    public static string ThemeEditorNewTitle => T("New theme", "Nuevo tema",
        "Neues Thema", "Nouveau thème", "Novo tema");
    public static string ThemeEditorNameLabel => T("Name", "Nombre",
        "Name", "Nom", "Nome");
    public static string ThemeDarkColors => T("Dark (light text)", "Oscuros (texto claro)",
        "Dunkel (heller Text)", "Sombres (texte clair)", "Escuras (texto claro)");
    public static string ThemeLightColors => T("Light (dark text)", "Claros (texto oscuro)",
        "Hell (dunkler Text)", "Claires (texte foncé)", "Claras (texto escuro)");
    public static string ThemeAddColor => T("Add color", "Añadir color",
        "Farbe hinzufügen", "Ajouter une couleur", "Adicionar cor");
    public static string ThemeMoveLeft => T("Move left", "Mover a la izquierda",
        "Nach links verschieben", "Déplacer à gauche", "Mover para a esquerda");
    public static string ThemeMoveRight => T("Move right", "Mover a la derecha",
        "Nach rechts verschieben", "Déplacer à droite", "Mover para a direita");
    public static string ThemeRemoveColor => T("Remove", "Quitar",
        "Entfernen", "Supprimer", "Remover");
    public static string ThemeEditorEmpty => T("Add at least one color.", "Añade al menos un color.",
        "Füge mindestens eine Farbe hinzu.", "Ajoutez au moins une couleur.", "Adicione pelo menos uma cor.");

    /// <summary>Los de serie se traducen; los propios se enseñan con el nombre que les dio el usuario.</summary>
    internal static string ThemeDisplayName(Aldune.Core.NoteTheme theme) => theme.Id switch
    {
        Aldune.Core.NoteThemes.ClassicId => T("Classic", "Clásico",
            "Klassisch", "Classique", "Clássico"),
        Aldune.Core.NoteThemes.SereneId => T("Serene", "Sereno",
            "Ruhig", "Serein", "Sereno"),
        Aldune.Core.NoteThemes.PastelId => T("Pastel", "Pastel",
            "Pastell", "Pastel", "Pastel"),
        Aldune.Core.NoteThemes.AutumnId => T("Autumn", "Otoño",
            "Herbst", "Automne", "Outono"),
        Aldune.Core.NoteThemes.OceanId => T("Ocean", "Océano",
            "Ozean", "Océan", "Oceano"),
        Aldune.Core.NoteThemes.XpId => T("XP", "XP", "XP", "XP", "XP"),
        _ => theme.Name,
    };

    public static string QuickHelpTitle => T("Quick help", "Ayuda rápida",
        "Schnellhilfe", "Aide rapide", "Ajuda rápida");
    public static string QuickHelpHover => T("• Hover over the screen edge to fan out the notes.", "• Pasa el ratón por el borde de la pantalla para desplegar las notas.",
        "• Bewege die Maus zum Bildschirmrand, um die Notizen aufzufächern.",
        "• Survolez le bord de l'écran pour déployer les notes.",
        "• Passe o mouse pela borda da tela para expandir as notas.");
    public static string QuickHelpDrag => T("• Click a tab to open that note; drag it yourself to move it — it will remember the spot.", "• Haz clic en una pestaña para abrir esa nota; arrástrala tú para moverla — recordará el sitio.",
        "• Klicke auf eine Registerkarte, um die Notiz zu öffnen; ziehe sie selbst, um sie zu verschieben — der Platz wird gespeichert.",
        "• Cliquez sur un onglet pour ouvrir la note ; faites-la glisser vous-même pour la déplacer — elle mémorisera la position.",
        "• Clique em uma aba para abrir a nota; arraste-a para movê-la — ela vai lembrar o lugar.");
    public static string QuickHelpRightClick => T("• Right-click a tab: change color, archive, or send to trash without opening it.", "• Clic derecho en una pestaña: cambiar color, archivar o tirar a la papelera sin abrirla.",
        "• Rechtsklick auf eine Registerkarte: Farbe ändern, archivieren oder in den Papierkorb verschieben, ohne sie zu öffnen.",
        "• Clic droit sur un onglet : changer la couleur, archiver ou mettre à la corbeille sans l'ouvrir.",
        "• Clique com o botão direito em uma aba: mudar cor, arquivar ou enviar para a lixeira sem abri-la.");
    public static string SettingsPageGeneral => T("General", "General",
        "Allgemein", "Général", "Geral");
    public static string SettingsPageNotes => T("Notes and tasks", "Notas y tareas",
        "Notizen und Aufgaben", "Notes et tâches", "Notas e tarefas");
    public static string SettingsPageDock => T("Dock and screens", "Dock y pantallas",
        "Dock und Bildschirme", "Dock et écrans", "Dock e telas");
    public static string SettingsPageColors => T("Colors", "Colores",
        "Farben", "Couleurs", "Cores");
    public static string SettingsPageSync => T("Sync", "Sincronización",
        "Synchronisierung", "Synchronisation", "Sincronização");
    public static string SettingsPageHelp => T("Help", "Ayuda",
        "Hilfe", "Aide", "Ajuda");
    public static string SettingsPageAbout => T("About", "Acerca de",
        "Info", "À propos", "Sobre");
    public static string QuickHelpDockGroup => T("The dock", "El dock",
        "Das Dock", "Le dock", "O dock");
    public static string QuickHelpNotesGroup => T("Notes", "Notas",
        "Notizen", "Notes", "Notas");
    public static string QuickHelpTasksGroup => T("Tasks and lists", "Tareas y listas",
        "Aufgaben und Listen", "Tâches et listes", "Tarefas e listas");
    public static string QuickHelpSyncGroup => T("Sync", "Sincronización",
        "Synchronisierung", "Synchronisation", "Sincronização");
    public static string QuickHelpTask => T("• Ctrl+L turns a line into a task. Click the checkbox (or press Ctrl+Enter) to check it off, and Enter keeps the list going on its own.", "• Ctrl+L convierte una línea en tarea. Haz clic en la casilla (o pulsa Ctrl+Enter) para marcarla, y Enter sigue la lista sola.",
        "• Ctrl+L verwandelt eine Zeile in eine Aufgabe. Klicke auf das Kästchen (oder drücke Ctrl+Enter), um sie abzuhaken, und Enter setzt die Liste selbst fort.",
        "• Ctrl+L transforme une ligne en tâche. Cliquez sur la case (ou appuyez sur Ctrl+Enter) pour la cocher, et Enter continue la liste tout seul.",
        "• Ctrl+L converte uma linha em tarefa. Clique na caixa (ou pressione Ctrl+Enter) para marcá-la, e Enter continua a lista sozinha.");
    public static string QuickHelpBullets => T("• Ctrl+Shift+L makes a bulleted list. Tab and Shift+Tab indent a line, for subtasks.", "• Ctrl+Shift+L hace una lista con viñetas. Tab y Mayús+Tab sangran la línea, para subtareas.",
        "• Ctrl+Shift+L erstellt eine Liste mit Aufzählungszeichen. Tab und Shift+Tab rücken eine Zeile ein, für Unteraufgaben.",
        "• Ctrl+Shift+L crée une liste à puces. Tab et Shift+Tab indentent une ligne, pour les sous-tâches.",
        "• Ctrl+Shift+L faz uma lista com marcadores. Tab e Shift+Tab recuam a linha, para subtarefas.");
    public static string QuickHelpLists => T("• For a list you reuse, like the shopping list: ⋯ > Uncheck all, or Remove checked tasks. In Settings, checked tasks can move to the end. Pasting a Markdown list (- [ ]) brings its checkboxes.", "• Para una lista que se repite, como la de la compra: ⋯ > Desmarcar todas, o Borrar las tareas hechas. En Ajustes, las hechas pueden bajar al final. Pegar una lista de Markdown (- [ ]) trae sus casillas.",
        "• Für eine Liste, die du immer wieder verwendest, wie die Einkaufsliste: ⋯ > Alle abhaken rückgängig machen oder Abgehakte Aufgaben entfernen. In den Einstellungen können abgehakte Aufgaben ans Ende verschoben werden. Das Einfügen einer Markdown-Liste (- [ ]) bringt ihre Kontrollkästchen mit.",
        "• Pour une liste réutilisable, comme la liste de courses : ⋯ > Décocher tout ou Supprimer les tâches cochées. Dans les paramètres, les tâches cochées peuvent passer en bas. Coller une liste Markdown (- [ ]) importe ses cases.",
        "• Para uma lista que você reutiliza, como a lista de compras: ⋯ > Desmarcar todas ou Remover tarefas concluídas. Em Configurações, as concluídas podem ir para o final. Colar uma lista Markdown (- [ ]) traz suas caixinhas.");
    public static string QuickHelpReminder => T("• ⋯ > Reminder notifies you at a set time, and the notice lists what's still to do.", "• ⋯ > Recordatorio te avisa a una hora, y el aviso dice qué tareas faltan.",
        "• ⋯ > Erinnerung benachrichtigt dich zu einer festgelegten Zeit, und die Benachrichtigung listet auf, was noch zu erledigen ist.",
        "• ⋯ > Rappel vous avertit à une heure définie, et la notification liste ce qu'il reste à faire.",
        "• ⋯ > Lembrete avisa você em um horário definido, e o aviso lista o que ainda falta fazer.");
    public static string QuickHelpMoveLine => T("• Alt+Up/Down moves the current line up or down — handy for reordering a checklist.", "• Alt+Arriba/Abajo sube o baja la línea del cursor — útil para reordenar una lista de tareas.",
        "• Alt+Auf/Ab verschiebt die aktuelle Zeile nach oben oder unten — praktisch zum Umsortieren einer Checkliste.",
        "• Alt+Haut/Bas déplace la ligne actuelle vers le haut ou le bas — pratique pour réordonner une liste.",
        "• Alt+Cima/Baixo move a linha atual para cima ou para baixo — útil para reordenar uma lista de tarefas.");
    public static string QuickHelpMenu => T("• The ⋯ button on a note: tasks, reminder, color, “always on top,” password, tags, export, archive and trash.", "• El botón ⋯ de una nota: tareas, recordatorio, color, «siempre encima», contraseña, etiquetas, exportar, archivar y papelera.",
        "• Die Schaltfläche ⋯ einer Notiz: Aufgaben, Erinnerung, Farbe, „Immer im Vordergrund“, Passwort, Tags, Exportieren, Archivieren und Papierkorb.",
        "• Le bouton ⋯ d'une note : tâches, rappel, couleur, « toujours au premier plan », mot de passe, étiquettes, exporter, archiver et corbeille.",
        "• O botão ⋯ de uma nota: tarefas, lembrete, cor, “sempre visível”, senha, etiquetas, exportar, arquivar e lixeira.");
    public static string QuickHelpEscape => T("• Esc closes the open note without losing what you wrote (it autosaves).", "• Esc cierra la nota abierta sin perder lo escrito (se guarda solo).",
        "• Esc schließt die geöffnete Notiz, ohne das Geschriebene zu verlieren (wird automatisch gespeichert).",
        "• Esc ferme la note ouverte sans perdre ce que vous avez écrit (sauvegarde automatique).",
        "• Esc fecha a nota aberta sem perder o que foi escrito (salva automaticamente).");
    public static string QuickHelpHotkeyOn(string combo) =>
        T($"• {combo} creates a new note from anywhere.", $"• {combo} crea una nota nueva desde cualquier sitio.",
            $"• {combo} erstellt eine neue Notiz von überall.",
            $"• {combo} crée une nouvelle note depuis n'importe où.",
            $"• {combo} cria uma nova nota de qualquer lugar.");
    public static string QuickHelpHotkeyOff => T("• The keyboard shortcut is off; turn it on above to create notes without going to the edge.", "• El atajo de teclado está desactivado; actívalo arriba para crear notas sin ir al borde.",
        "• Das Tastaturkürzel ist deaktiviert; aktiviere es oben, um Notizen zu erstellen, ohne zum Rand zu gehen.",
        "• Le raccourci clavier est désactivé ; activez-le ci-dessus pour créer des notes sans aller au bord.",
        "• O atalho de teclado está desativado; ative-o acima para criar notas sem ir até a borda.");
    public static string QuickHelpTray => T("• The tray icon opens the notes manager and these settings.", "• El icono de la bandeja abre el gestor de notas y estos ajustes.",
        "• Das Taskleistensymbol öffnet den Notizenmanager und diese Einstellungen.",
        "• L'icône de la barre des tâches ouvre le gestionnaire de notes et ces paramètres.",
        "• O ícone da bandeja abre o gerenciador de notas e estas configurações.");
    public static string QuickHelpHideDock => T(
        "• Ctrl+Alt+H hides the dock and brings it back — handy for fullscreen videos Windows doesn't report as such. The tray menu has the same switch.",
        "• Ctrl+Alt+H oculta el dock y lo devuelve — útil para vídeos a pantalla completa que Windows no reporta como tales. El menú de la bandeja tiene el mismo interruptor.",
        "• Ctrl+Alt+H blendet das Dock aus und wieder ein — praktisch für Vollbildvideos, die Windows nicht als solche erkennt. Das Taskleistenmenü hat denselben Schalter.",
        "• Ctrl+Alt+H masque le dock et le restaure — pratique pour les vidéos en plein écran que Windows ne signale pas comme telles. Le menu de la barre des tâches possède le même commutateur.",
        "• Ctrl+Alt+H oculta o dock e o exibe novamente — útil para vídeos em tela cheia que Windows não detecta como tal. O menu da bandeja tem o mesmo interruptor.");
    public static string QuickHelpDockMenus => T(
        "• Right-click the dock buttons for views, tags, notes from the clipboard, settings and Keep dock open.",
        "• Clic derecho en los botones del dock para ver vistas, etiquetas, notas desde el portapapeles, Ajustes y Mantener el dock abierto.",
        "• Rechtsklick auf die Dock-Schaltflächen für Ansichten, Tags, Notizen aus der Zwischenablage, Einstellungen und Dock offen halten.",
        "• Clic droit sur les boutons du dock pour les vues, les étiquettes, les notes depuis le presse-papiers, les paramètres et Garder le dock ouvert.",
        "• Clique com o botão direito nos botões do dock para ver vistas, etiquetas, notas da área de transferência, Configurações e Manter o dock aberto.");
    public static string QuickHelpAutoHideTasks => T(
        "• Completed tasks can disappear automatically after the delay configured in Settings.",
        "• Las tareas completadas pueden desaparecer automáticamente tras el plazo configurado en Ajustes.",
        "• Abgehakte Aufgaben können nach der in den Einstellungen konfigurierten Verzögerung automatisch verschwinden.",
        "• Les tâches cochées peuvent disparaître automatiquement après le délai configuré dans les paramètres.",
        "• As tarefas concluídas podem desaparecer automaticamente após o prazo configurado em Configurações.");
    public static string QuickHelpConflicts => T(
        "• Sync conflicts can be reviewed in Settings; restore the losing version or dismiss the record.",
        "• Los conflictos de sincronización se revisan en Ajustes; puedes restaurar la versión perdedora o descartar el registro.",
        "• Synchronisierungskonflikte können in den Einstellungen überprüft werden; stelle die unterlegene Version wieder her oder verwerfe den Eintrag.",
        "• Les conflits de synchronisation peuvent être examinés dans les paramètres ; restaurez la version perdante ou ignorez l'enregistrement.",
        "• Os conflitos de sincronização podem ser revisados em Configurações; restaure a versão perdedora ou descarte o registro.");
    public static string QuickHelpSync => T("• Sync now is available in the dock; automatic sync can be enabled in Settings.", "• Puedes sincronizar desde el dock y activar la sincronización automática en Ajustes.",
        "• Jetzt synchronisieren ist im Dock verfügbar; die automatische Synchronisierung kann in den Einstellungen aktiviert werden.",
        "• Synchroniser maintenant est disponible dans le dock ; la synchronisation automatique peut être activée dans les paramètres.",
        "• Sincronizar agora está disponível no dock; a sincronização automática pode ser ativada em Configurações.");
    public static string QuickHelpSearch => T("• In Manage notes, Ctrl+F focuses the search box and selects the current search.", "• En Gestionar notas, Ctrl+F enfoca la búsqueda y selecciona el texto actual.",
        "• In Notizen verwalten setzt Ctrl+F den Fokus auf das Suchfeld und markiert die aktuelle Suche.",
        "• Dans Gérer les notes, Ctrl+F met le focus sur la zone de recherche et sélectionne la recherche actuelle.",
        "• Em Gerenciar notas, Ctrl+F foca a caixa de pesquisa e seleciona a busca atual.");
    public static string QuickHelpAutoScroll => T(
        "• Middle-click on a long list or note turns on autoscroll: the view follows the cursor on its own; another click stops it.",
        "• El clic central sobre una lista larga o una nota activa el autodesplazamiento: la vista sigue al cursor sola; otro clic lo apaga.",
        "• Ein mittlerer Mausklick auf eine lange Liste oder Notiz aktiviert das automatische Scrollen: Die Ansicht folgt dem Cursor automatisch; ein weiterer Klick hält es an.",
        "• Un clic du milieu sur une longue liste ou une note active le défilement automatique : la vue suit le curseur toute seule ; un autre clic l'arrête.",
        "• O clique do meio em uma lista longa ou nota ativa a rolagem automática: a visualização segue o cursor sozinha; outro clique a desliga.");

    public static string MinimizeWindowTooltip => T("Minimize", "Minimizar",
        "Minimieren", "Réduire", "Minimizar");
    public static string MaximizeWindowTooltip => T("Maximize", "Maximizar",
        "Maximieren", "Agrandir", "Maximizar");
    public static string RestoreWindowTooltip => T("Restore", "Restaurar",
        "Wiederherstellen", "Restaurer", "Restaurar");

    // --- Arranque / errores de arranque --------------------------------------------------------------

    public static string UnexpectedErrorTitle => T("Aldune — error", "Aldune — error",
        "Aldune — Fehler", "Aldune — erreur", "Aldune — erro");
    public static string UnexpectedErrorMessage(string details) =>
        T($"An unexpected error occurred: {details}\n\nThe application will continue, but this particular action may not have completed.",
          $"Ha ocurrido un error inesperado: {details}\n\nLa aplicación continuará, pero esta acción concreta puede no haberse completado.",
            $"Ein unerwarteter Fehler ist aufgetreten: {details}\\n\\nDie Anwendung wird fortgesetzt, aber diese bestimmte Aktion wurde möglicherweise nicht abgeschlossen.",
            $"Une erreur inattendue s'est produite : {details}\\n\\nL'application continuera, mais cette action en particulier peut ne pas s'être terminée.",
            $"Ocorreu um erro inesperado: {details}\\n\\nO aplicativo continuará, mas esta ação específica pode não ter sido concluída.");

    public static string CannotStartTitle => T("Aldune — can't start", "Aldune — no se puede iniciar",
        "Aldune — Start nicht möglich", "Aldune — impossible de démarrer", "Aldune — não é possível iniciar");
    public static string DatabaseRecoveredTitle => T("Aldune — database recovered", "Aldune — base de datos recuperada",
        "Aldune — Datenbank wiederhergestellt",
        "Aldune — base de données restaurée",
        "Aldune — banco de dados recuperado");

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
        "O banco de dados de notas não pode ser descriptografado com a chave armazenada.\n\n" + "A causa mais provável é que a senha deste usuário do Windows foi redefinida, o que destrói permanentemente a chave protegida. Os backups automáticos são protegidos da mesma forma, portanto também não conseguem recuperá-la.");

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
        "O arquivo de configurações que guarda a chave das suas notas está ausente e nenhum backup o possui.\\n\\n" + "Aldune não criará uma nova chave, pois suas notas existentes ficariam ilegíveis. Se você " + $"tiver uma cópia de settings.json, coloque-a em {folder} e abra o Aldune novamente.\\n\\n" + $"Para começar do zero, mova notes.db de {folder}: Aldune criará um novo, vazio.");

    public static string KeyRestoredFromBackupMessage(string day) => T(
        $"Your notes' key was missing from the settings and has been restored from the backup of {day}.",
        $"La clave de tus notas faltaba en los ajustes y se ha recuperado de la copia del {day}.",
        $"Der Schlüssel deiner Notizen fehlte in den Einstellungen und wurde aus der Sicherung vom {day} wiederhergestellt.",
        $"La clé de vos notes était absente des paramètres et a été restaurée à partir de la sauvegarde du {day}.",
        $"A chave das suas notas estava ausente nas configurações e foi recuperada do backup de {day}.");

    public static string SettingsRestoredFromBackupMessage(string day) => T(
        $"The settings file was damaged and the backup of {day} has been restored. Check any setting you changed after that day; if sync fails, import your sync code again.",
        $"El archivo de ajustes estaba dañado y se ha restaurado la copia del {day}. Revisa los ajustes que cambiaras después de ese día; si la sincronización falla, vuelve a importar tu código.",
        $"Die Einstellungsdatei war beschädigt und die Sicherung vom {day} wurde wiederhergestellt. Überprüfe alle Einstellungen, die du nach diesem Tag geändert hast; falls die Synchronisierung fehlschlägt, importiere deinen Code erneut.",
        $"Le fichier de paramètres était endommagé et la sauvegarde du {day} a été restaurée. Vérifiez les paramètres modifiés après ce jour ; si la synchronisation échoue, réimportez votre code.",
        $"O arquivo de configurações estava danificado e o backup de {day} foi restaurado. Verifique qualquer configuração que você tenha alterado após esse dia; se a sincronização falhar, importe seu código novamente.");

    public static string DataRecoveredTitle => T("Aldune — data recovered", "Aldune — datos recuperados",
        "Aldune — Daten wiederhergestellt", "Aldune — données restaurées", "Aldune — dados recuperados");

    public static string DatabaseUnrecoverableMessage => T(
        "The notes database couldn't be opened or recreated after an automatic recovery attempt. " +
        "The disk may be full, or the file may still be damaged.",
        "No se ha podido abrir ni recrear la base de datos de notas tras un intento de " +
        "recuperación automática. Es posible que el disco esté lleno o que el archivo siga " +
        "dañado.",
        "Die Notizdatenbank konnte nach einem automatischen Wiederherstellungsversuch nicht geöffnet oder neu erstellt werden. " + "Eventuell ist die Festplatte voll oder die Datei ist noch beschädigt.",
        "La base de données de notes n'a pas pu être ouverte ni recréée après une tentative de restauration automatique. " + "Le disque est peut-être plein ou le fichier est encore endommagé.",
        "O banco de dados de notas não pôde ser aberto nem recriado após uma tentativa de recuperação automática. " + "O disco pode estar cheio ou o arquivo ainda pode estar danificado.");

    public static string UnexpectedStartupFailureMessage(string details) =>
        T($"Aldune couldn't start due to an unexpected error: {details}",
          $"No se ha podido iniciar Aldune debido a un error inesperado: {details}",
            $"Aldune konnte aufgrund eines unerwarteten Fehlers nicht gestartet werden: {details}",
            $"Aldune n'a pas pu démarrer en raison d'une erreur inattendue : {details}",
            $"Não foi possível iniciar o Aldune devido a um erro inesperado: {details}");

    public static string NoMonitorsMessage => T("No connected monitor could be detected.", "No se ha podido detectar ningún monitor conectado.",
        "Es konnte kein angeschlossener Monitor erkannt werden.",
        "Aucun moniteur connecté n'a pu être détecté.",
        "Nenhum monitor conectado pôde ser detectado.");

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
        "O banco de dados de notas não pôde ser descriptografado com a chave atual.\n\n" + "A causa mais provável é que o arquivo de configuração que continha a chave foi perdido, substituído ou vem de outra instalação. Suas notas NÃO foram excluídas e permanecem armazenadas com segurança, mas não podem ser lidas agora.");

    public static string DatabaseRecoveredMessage => T(
        "The existing notes file was damaged. A copy was kept in a \".corrupt-<date>\" file next " +
        "to the original, and a fresh, empty database was created.",
        "El archivo de notas existente estaba dañado. Se ha conservado una copia en un archivo " +
        "\".corrupt-<fecha>\" junto al original, y se ha creado una base de datos nueva y vacía.",
        "Die vorhandene Notizdatei war beschädigt. Eine Kopie wurde in einer Datei \".corrupt-<Datum>\" neben dem Original aufbewahrt, und eine neue, leere Datenbank wurde erstellt.",
        "Le fichier de notes existant était endommagé. Une copie a été conservée dans un fichier \".corrupt-<date>\" à côté de l'original, et une nouvelle base de données vide a été créée.",
        "O arquivo de notas existente estava danificado. Uma cópia foi mantida em um arquivo \".corrupt-<data>\" ao lado do original, e um novo banco de dados vazio foi criado.");

    // --- Notas vinculadas a archivos ---
    public static string LinkedOpenFile => T("Open file…", "Abrir archivo…", "Datei öffnen…", "Ouvrir un fichier…", "Abrir arquivo…");
    public static string LinkedFileFilter => T("Text and Markdown files", "Archivos de texto y Markdown",
        "Text- und Markdown-Dateien", "Fichiers texte et Markdown", "Arquivos de texto e Markdown")
        + " (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt";
    public static string LinkedUnsupported(string name) => T(
        $"{name} isn't a .md, .markdown or .txt file.", $"{name} no es un archivo .md, .markdown ni .txt.",
        $"{name} ist keine .md-, .markdown- oder .txt-Datei.", $"{name} n'est pas un fichier .md, .markdown ou .txt.",
        $"{name} não é um arquivo .md, .markdown nem .txt.");
    public static string LinkedTooLarge(string name) => T(
        $"{name} is larger than 2 MB and can't be opened as a note.", $"{name} ocupa más de 2 MB y no se puede abrir como nota.",
        $"{name} ist größer als 2 MB und kann nicht als Notiz geöffnet werden.", $"{name} dépasse 2 Mo et ne peut pas être ouvert comme note.",
        $"{name} tem mais de 2 MB e não pode ser aberto como nota.");
    public static string LinkedBadEncoding(string name) => T(
        $"{name} isn't UTF-8 or UTF-16 text. Aldune won't open it so as not to damage its accents.",
        $"{name} no está en UTF-8 ni en UTF-16. Aldune no lo abre para no estropear sus tildes.",
        $"{name} ist kein UTF-8- oder UTF-16-Text. Aldune öffnet die Datei nicht, um Sonderzeichen nicht zu beschädigen.",
        $"{name} n'est pas en UTF-8 ni en UTF-16. Aldune ne l'ouvre pas pour ne pas abîmer ses accents.",
        $"{name} não está em UTF-8 nem em UTF-16. O Aldune não o abre para não estragar os acentos.");
    public static string LinkedUnreadable(string name) => T(
        $"{name} couldn't be read.", $"No se pudo leer {name}.", $"{name} konnte nicht gelesen werden.",
        $"Impossible de lire {name}.", $"Não foi possível ler {name}.");
    public static string LinkedAlreadyLinkedElsewhere(string name) => T(
        $"{name} is already open in another note.", $"{name} ya está abierto en otra nota.",
        $"{name} ist bereits in einer anderen Notiz geöffnet.", $"{name} est déjà ouvert dans une autre note.",
        $"{name} já está aberto em outra nota.");
    public static string LinkedUnavailable(string path) => T(
        $"File not available: {path}", $"Archivo no disponible: {path}", $"Datei nicht verfügbar: {path}",
        $"Fichier indisponible : {path}", $"Arquivo indisponível: {path}");
    public static string LinkedRetry => T("Retry", "Reintentar", "Erneut versuchen", "Réessayer", "Tentar de novo");
    public static string LinkedLocate => T("Find…", "Buscar…", "Suchen…", "Rechercher…", "Procurar…");
    public static string LinkedConvert => T("Convert to normal note", "Convertir en nota normal",
        "In normale Notiz umwandeln", "Convertir en note normale", "Converter em nota normal");
    public static string LinkedConvertWarning(string path) => T(
        $"The note keeps its text and stops following the file. The file stays on disk, as plain text:\n{path}",
        $"La nota conserva su texto y deja de seguir al archivo. El archivo sigue en el disco, en claro:\n{path}",
        $"Die Notiz behält ihren Text und folgt der Datei nicht mehr. Die Datei bleibt unverschlüsselt auf dem Datenträger:\n{path}",
        $"La note garde son texte et ne suit plus le fichier. Le fichier reste sur le disque, en clair :\n{path}",
        $"A nota mantém o texto e deixa de acompanhar o arquivo. O arquivo continua no disco, sem criptografia:\n{path}");
    public static string LinkedConflictPrefix => T("⚠ Conflict: ", "⚠ Conflicto: ", "⚠ Konflikt: ", "⚠ Conflit : ", "⚠ Conflito: ");
    public static string LinkedConflictToast(string title) => T(
        $"{title} changed in another program while you were editing it here. Your version is in a new note.",
        $"{title} cambió en otro programa mientras lo editabas aquí. Tu versión está en una nota nueva.",
        $"{title} wurde in einem anderen Programm geändert, während du es hier bearbeitet hast. Deine Version steht in einer neuen Notiz.",
        $"{title} a été modifié dans un autre programme pendant que vous l'éditiez ici. Votre version est dans une nouvelle note.",
        $"{title} mudou em outro programa enquanto você o editava aqui. Sua versão está em uma nota nova.");
    public static string LinkedSyncThisNote => T("Sync this note", "Sincronizar esta nota",
        "Diese Notiz synchronisieren", "Synchroniser cette note", "Sincronizar esta nota");
    public static string LinkedOpenInEditor => T("Open in its editor", "Abrir en su editor",
        "Im zugehörigen Editor öffnen", "Ouvrir dans son éditeur", "Abrir no editor padrão");
    public static string LinkedShowInExplorer => T("Show in File Explorer", "Mostrar en el Explorador",
        "Im Explorer anzeigen", "Afficher dans l'Explorateur", "Mostrar no Explorador");
    public static string LinkedSaveAs => T("Save as linked file…", "Guardar como archivo vinculado…",
        "Als verknüpfte Datei speichern…", "Enregistrer comme fichier lié…", "Salvar como arquivo vinculado…");
    public static string LinkedSaveAsExists(string name) => T(
        $"{name} already exists. Aldune doesn't replace files: choose another name.",
        $"{name} ya existe. Aldune no reemplaza archivos: elige otro nombre.",
        $"{name} existiert bereits. Aldune ersetzt keine Dateien: Wähle einen anderen Namen.",
        $"{name} existe déjà. Aldune ne remplace pas de fichiers : choisissez un autre nom.",
        $"{name} já existe. O Aldune não substitui arquivos: escolha outro nome.");
    public static string LinkedProtectDisabled => T(
        "A linked note is the file itself, and the file is plain text. Convert it to a normal note to protect it.",
        "Una nota vinculada es el propio archivo, y el archivo está en claro. Conviértela en nota normal para protegerla.",
        "Eine verknüpfte Notiz ist die Datei selbst, und die Datei ist unverschlüsselt. Wandle sie in eine normale Notiz um, um sie zu schützen.",
        "Une note liée est le fichier lui-même, et le fichier est en clair. Convertissez-la en note normale pour la protéger.",
        "Uma nota vinculada é o próprio arquivo, e o arquivo está sem criptografia. Converta-a em nota normal para protegê-la.");
}
