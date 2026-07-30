/// <summary>
/// UI + category overlays for every VS language (quest.* lore stays English except ru.json).
/// </summary>
internal static class UiTranslations
{
    public static readonly Dictionary<string, Dictionary<string, string>> All = Build();

    static Dictionary<string, Dictionary<string, string>> Build()
    {
        var all = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string lang, Dictionary<string, string> map) => all[lang] = map;

        Add("de", De());
        Add("fr", Fr());
        Add("es-es", Es());
        Add("es-419", EsLatam());
        Add("pl", Pl());
        Add("uk", Uk());
        Add("be", Be());
        Add("pt-br", PtBr());
        Add("pt-pt", PtPt());
        Add("it", It());
        Add("cs", Cs());
        Add("sk", Sk());
        Add("nl", Nl());
        Add("da", Da());
        Add("no", No());
        Add("sv-se", Sv());
        Add("fi", Fi());
        Add("hu", Hu());
        Add("ro", Ro());
        Add("tr", Tr());
        Add("ja", Ja());
        Add("ko", Ko());
        Add("zh-cn", ZhCn());
        Add("zh-tw", ZhTw());
        Add("ar", Ar());
        Add("eo", Eo());
        Add("is", Is());
        Add("lt", Lt());
        Add("sr", Sr());
        Add("th", Th());
        Add("vi", Vi());
        return all;
    }

    // ── helpers: shared category pack builders ───────────────────────────

    static void Categories(
        Dictionary<string, string> d,
        string legend, string legendH,
        string stone, string stoneH,
        string clay, string clayH,
        string copper, string copperH,
        string bronze, string bronzeH,
        string iron, string ironH,
        string steel, string steelH,
        string hunting, string huntingH,
        string farming, string farmingH,
        string crafts, string craftsH,
        string explore, string exploreH,
        string branch, string branchH)
    {
        d["category.legend.title"] = legend;
        d["category.legend.header"] = legendH;
        d["category.stone.title"] = stone;
        d["category.stone.header"] = stoneH;
        d["category.clay.title"] = clay;
        d["category.clay.header"] = clayH;
        d["category.copper.title"] = copper;
        d["category.copper.header"] = copperH;
        d["category.bronze.title"] = bronze;
        d["category.bronze.header"] = bronzeH;
        d["category.iron.title"] = iron;
        d["category.iron.header"] = ironH;
        d["category.steel.title"] = steel;
        d["category.steel.header"] = steelH;
        d["category.hunting.title"] = hunting;
        d["category.hunting.header"] = huntingH;
        d["category.farming.title"] = farming;
        d["category.farming.header"] = farmingH;
        d["category.crafts.title"] = crafts;
        d["category.crafts.header"] = craftsH;
        d["category.explore.title"] = explore;
        d["category.explore.header"] = exploreH;
        d["category.new_branch.title"] = branch;
        d["category.new_branch.header"] = branchH;
    }

    static Dictionary<string, string> Pack(
        string title, string close, string empty, string hotkey,
        // modal
        string mQuest, string mCompleted, string mGoals, string mRewards, string mInfo,
        string mStart, string mStarted, string mProc, string mPassed, string mContinue, string mReceived, string mClaim,
        string gCraft, string gCraftTurn, string gTurn, string gObtain, string gKill,
        string hCraft, string hCraftTurn, string hTurn, string hObtain, string hKill, string hMixed,
        string craftProg,
        // admin common
        string addItem, string selectPreset, string goalsId, string awardsId, string num, string infoText,
        string direction, string presetQuests, string parentNode,
        string add, string delete, string clear, string save,
        string pStart, string pQuest, string pCheckpoint, string pKill, string editMode,
        string modeBranches, string modeQuests,
        string branchStatus, string branchHint, string listHeader, string listNamed,
        string addBranch, string addBranchTitle, string nameLabel, string langLabel, string namePh, string headerHint,
        string create, string cancel, string renameBranch,
        string iconLabel, string iconHint, string iconPicker, string iconClose,
        string deleteBranch, string renameTitle, string renameSave, string deleteTitle, string deleteConfirm, string deleteBtn,
        string closeEditor,
        string toolSelect, string toolNew, string toolLine, string toolDelete, string toolGrid,
        string iSelect, string iNew, string iLink, string iDelete, string iSave, string iClear, string iEdit,
        string iGrid, string iClose, string iBranches, string iQuests, string iAdd, string iRename, string iDelBranch,
        string iImage, string iEditor, string iStart, string iQuest, string iCheckpoint, string iKill,
        string stPickSelect, string stSelected, string stPickEdit, string stPickNew, string stLineSrc, string stLineTgt,
        string stPickDel, string stNoSel, string stEditing, string stClickEdit, string stSelectTool,
        string qeHeader, string qeTitle, string qeGoals, string qeKill, string qeAwards, string qeEmpty,
        string qePick, string qePickerTitle, string qePickerCancel, string qeSearchPh, string qeSearchEmpty,
        string qeAllTypes, string qeExact, string qeConsumeReq, string qeConsumeOn, string qeConsumeOff,
        string qeCraftGoal, string qeHaveGoal, string qeLegend,
        string fTake, string fCraft, string fVariants, string fKill,
        string qePickEntity, string qeEntityTitle, string qeEntityPh, string qeEntityEmpty,
        string qeCount, string qeSave)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = title,
            ["title_bar"] = "{0} · {1}%",
            ["close"] = close,
            ["empty_category"] = empty,
            ["hotkey_name"] = hotkey,
            ["modal.quest_prefix"] = mQuest,
            ["modal.completed"] = mCompleted,
            ["modal.goals"] = mGoals,
            ["modal.rewards"] = mRewards,
            ["modal.info_placeholder"] = mInfo,
            ["modal.start_path"] = mStart,
            ["modal.path_started"] = mStarted,
            ["modal.processing"] = mProc,
            ["modal.passed"] = mPassed,
            ["modal.continue"] = mContinue,
            ["modal.received"] = mReceived,
            ["modal.claim_reward"] = mClaim,
            ["modal.goal.craft"] = gCraft,
            ["modal.goal.craft_turn_in"] = gCraftTurn,
            ["modal.goal.turn_in"] = gTurn,
            ["modal.goal.obtain"] = gObtain,
            ["modal.goal.kill"] = gKill,
            ["modal.goal.progress"] = "{0}/{1}",
            ["modal.goals_hint.craft"] = hCraft,
            ["modal.goals_hint.craft_turn_in"] = hCraftTurn,
            ["modal.goals_hint.turn_in"] = hTurn,
            ["modal.goals_hint.obtain"] = hObtain,
            ["modal.goals_hint.kill"] = hKill,
            ["modal.goals_hint.mixed"] = hMixed,
            ["modal.craft_progress"] = craftProg,
            ["admin.add_an_item"] = addItem,
            ["admin.select_a_preset"] = selectPreset,
            ["admin.goals_id"] = goalsId,
            ["admin.awards_id"] = awardsId,
            ["admin.num"] = num,
            ["admin.information_text"] = infoText,
            ["admin.choose_direction"] = direction,
            ["admin.preset_quests"] = presetQuests,
            ["admin.parent_node"] = parentNode,
            ["admin.add"] = add,
            ["admin.delete"] = delete,
            ["admin.clear"] = clear,
            ["admin.save"] = save,
            ["admin.preset.start"] = pStart,
            ["admin.preset.quest"] = pQuest,
            ["admin.preset.checkpoint"] = pCheckpoint,
            ["admin.preset.kill"] = pKill,
            ["admin.edit_mode"] = editMode,
            ["admin.mode.branches"] = modeBranches,
            ["admin.mode.quests"] = modeQuests,
            ["admin.branch.status"] = branchStatus,
            ["admin.branch.hint"] = branchHint,
            ["admin.branch.list_header"] = listHeader,
            ["admin.branch.list_header_named"] = listNamed,
            ["admin.add_branch"] = addBranch,
            ["admin.add_branch.title"] = addBranchTitle,
            ["admin.add_branch.name_label"] = nameLabel,
            ["admin.add_branch.lang_label"] = langLabel,
            ["admin.add_branch.name_placeholder"] = namePh,
            ["admin.add_branch.header_hint"] = headerHint,
            ["admin.add_branch.create"] = create,
            ["admin.add_branch.cancel"] = cancel,
            ["admin.rename_branch"] = renameBranch,
            ["admin.branch_icon.label"] = iconLabel,
            ["admin.branch_icon.hint"] = iconHint,
            ["admin.branch_icon.picker_title"] = iconPicker,
            ["admin.branch_icon.picker_close"] = iconClose,
            ["admin.delete_branch"] = deleteBranch,
            ["admin.rename_branch.title"] = renameTitle,
            ["admin.rename_branch.save"] = renameSave,
            ["admin.delete_branch.title"] = deleteTitle,
            ["admin.delete_branch.confirm"] = deleteConfirm,
            ["admin.delete_branch.confirm_button"] = deleteBtn,
            ["admin.close_editor"] = closeEditor,
            ["admin.tool.select"] = toolSelect,
            ["admin.tool.new"] = toolNew,
            ["admin.tool.line"] = toolLine,
            ["admin.tool.delete"] = toolDelete,
            ["admin.tool.grid"] = toolGrid,
            ["admin.icon.select"] = iSelect,
            ["admin.icon.new"] = iNew,
            ["admin.icon.link"] = iLink,
            ["admin.icon.delete"] = iDelete,
            ["admin.icon.save"] = iSave,
            ["admin.icon.clear"] = iClear,
            ["admin.icon.edit_quest"] = iEdit,
            ["admin.icon.grid"] = iGrid,
            ["admin.icon.close"] = iClose,
            ["admin.icon.branches"] = iBranches,
            ["admin.icon.quests"] = iQuests,
            ["admin.icon.add"] = iAdd,
            ["admin.icon.rename"] = iRename,
            ["admin.icon.delete_branch"] = iDelBranch,
            ["admin.icon.image"] = iImage,
            ["admin.icon.editor"] = iEditor,
            ["admin.icon.start"] = iStart,
            ["admin.icon.quest"] = iQuest,
            ["admin.icon.checkpoint"] = iCheckpoint,
            ["admin.icon.kill"] = iKill,
            ["admin.status.pick_select"] = stPickSelect,
            ["admin.status.selected"] = stSelected,
            ["admin.status.pick_edit"] = stPickEdit,
            ["admin.status.pick_new_position"] = stPickNew,
            ["admin.status.pick_line_source"] = stLineSrc,
            ["admin.status.pick_line_target"] = stLineTgt,
            ["admin.status.pick_delete"] = stPickDel,
            ["admin.status.no_selection"] = stNoSel,
            ["admin.status.editing"] = stEditing,
            ["admin.status.click_quest_edit"] = stClickEdit,
            ["admin.status.select_tool"] = stSelectTool,
            ["admin.quest_edit.header"] = qeHeader,
            ["admin.quest_edit.title"] = qeTitle,
            ["admin.quest_edit.goals_section"] = qeGoals,
            ["admin.quest_edit.kill_section"] = qeKill,
            ["admin.quest_edit.awards_section"] = qeAwards,
            ["admin.quest_edit.empty_list"] = qeEmpty,
            ["admin.quest_edit.pick_item"] = qePick,
            ["admin.quest_edit.picker_title"] = qePickerTitle,
            ["admin.quest_edit.picker_cancel"] = qePickerCancel,
            ["admin.quest_edit.item_search_placeholder"] = qeSearchPh,
            ["admin.quest_edit.item_search_empty"] = qeSearchEmpty,
            ["admin.quest_edit.match_all_variants"] = qeAllTypes,
            ["admin.quest_edit.match_exact"] = qeExact,
            ["admin.quest_edit.consume_required"] = qeConsumeReq,
            ["admin.quest_edit.consume_on"] = qeConsumeOn,
            ["admin.quest_edit.consume_off"] = qeConsumeOff,
            ["admin.quest_edit.craft_goal"] = qeCraftGoal,
            ["admin.quest_edit.have_goal"] = qeHaveGoal,
            ["admin.quest_edit.goals_legend"] = qeLegend,
            ["admin.quest_edit.flag.take"] = fTake,
            ["admin.quest_edit.flag.craft"] = fCraft,
            ["admin.quest_edit.flag.variants"] = fVariants,
            ["admin.quest_edit.flag.kill"] = fKill,
            ["admin.quest_edit.pick_entity"] = qePickEntity,
            ["admin.quest_edit.entity_picker_title"] = qeEntityTitle,
            ["admin.quest_edit.entity_search_placeholder"] = qeEntityPh,
            ["admin.quest_edit.entity_search_empty"] = qeEntityEmpty,
            ["admin.quest_edit.count_label"] = qeCount,
            ["admin.quest_edit.save"] = qeSave,
        };
        return d;
    }

    // ── per-language ─────────────────────────────────────────────────────

    static Dictionary<string, string> De()
    {
        var d = Pack(
            "QUESTS", "SCHLIESSEN", "Dieser Zweig hat noch keine Quests.", "Questtagebuch",
            "QUEST: ", "ABGESCHLOSSEN", "Ziele:", "Belohnungen:", "Infotext",
            "PFAD STARTEN", "PFAD GESTARTET", "VERARBEITUNG...", "BESTANDEN", "WEITER", "ERHALTEN", "BELOHNUNG HOLEN",
            "Herstellen", "Herstellen & abgeben", "Abgeben", "Besitzen", "Töten",
            "Gegenstände herstellen", "Herstellen und abgeben", "Gegenstände für Belohnung abgeben",
            "Gegenstände besitzen (werden nicht entnommen)", "Kreaturen töten", "Siehe Beschriftung unter jedem Gegenstand",
            "Hergestellt: {0}/{1}",
            "ELEMENT HINZUFÜGEN", "VORLAGE WÄHLEN", "Ziele_ID {0}", "Belohnungen_ID {0}", "(Anz.)", "Infotext",
            "Richtung | ", "Quest-Vorlage | ", "Knoten | ",
            "HINZU", "LÖSCHEN", "LEEREN", "SPEICHERN",
            "START", "QUEST", "CHECKPOINT", "TÖTEN", "EDITOR",
            "ZWEIGE", "QUESTS",
            "Zweig: {0}", "Zweig wählen oder neu erstellen", "ZWEIG WÄHLEN", "Zweig \"{0}\"",
            "+ ZWEIG", "Neuer Questzweig", "Zweigname", "Sprache", "Zweigname eingeben", "Kopfzeile: {0}",
            "ERSTELLEN", "ABBRECHEN", "BEARB.",
            "Klicken zum Wählen", "Klicken zum Wählen", "Symbol wählen", "SCHLIESSEN",
            "LÖSCHEN", "Zweig bearbeiten", "SPEICHERN", "Zweig löschen?", "Zweig \"{0}\" löschen? Das kann nicht rückgängig gemacht werden.", "LÖSCHEN",
            "SCHLIESSEN",
            "AUSWAHL", "NEU", "LINIE", "LÖSCHEN", "RASTER",
            "AUSWAHL", "NEUE QUEST", "VERKN.", "LÖSCHEN", "SPEICHERN", "RESET", "BEARB.",
            "RASTER", "EXIT", "ZWEIGE", "QUESTS", "ERSTELLEN", "BEARB.", "ZWEIG LÖSCHEN",
            "Symbol", "Editor", "START", "QUEST", "CHECKP.", "TÖTEN",
            "Quests zum Verschieben ziehen", "Gewählt #{0}", "Quest zum Bearbeiten anklicken", "Graph klicken für neue Quest",
            "Link: Startknoten", "Link: Zielknoten", "Knoten zum Löschen klicken", "Knoten wählen oder erstellen",
            "Bearbeite #{0}", "Quest #{0}", "Werkzeug wählen",
            "Quest wählen", "Quest #{0}", "Ziele", "Töten", "Belohnungen", "Leer — + drücken",
            "Wählen", "Gegenstand wählen", "Abbrechen", "Gegenstände suchen…", "Keine Treffer",
            "Alle Typen", "Exakt", "Gegenstände entnehmen (aus = nur prüfen)", "Beim Abholen entnehmen", "Nur prüfen (bleiben)",
            "Herstellen", "Besitzen", "Icons: craft · alle Typen  |  entnehmen — oben",
            "Consume", "Craft", "Alle Typen", "Töten",
            "Klicken zum Wählen einer Kreatur", "Kreatur zum Töten wählen", "Kreaturen suchen…", "Keine Treffer",
            "Anz.", "SPEICHERN");
        Categories(d,
            "Legende", "EMBERSTAR-PFAD",
            "Steinzeit", "STEINZEIT", "Töpferei", "TÖPFEREI",
            "Kupferzeit", "KUPFERZEIT", "Bronzezeit", "BRONZEZEIT",
            "Eisenzeit", "EISENZEIT", "Stahlzeit", "STAHLZEIT",
            "Jagd", "JAGD", "Landwirtschaft", "LANDWIRTSCHAFT",
            "Handwerk", "HANDWERK", "Wanderungen", "WANDERUNGEN",
            "Zweig {0}", "ZWEIG {0}");
        return d;
    }

    static Dictionary<string, string> Fr()
    {
        var d = Pack(
            "QUÊTES", "FERMER", "Cette branche n'a pas encore de quêtes.", "Journal de quêtes",
            "QUÊTE : ", "TERMINÉE", "Objectifs :", "Récompenses :", "Texte d'information",
            "COMMENCER", "CHEMIN COMMENCÉ", "TRAITEMENT...", "RÉUSSI", "CONTINUER", "REÇU", "RÉCUPÉRER LA RÉCOMPENSE",
            "Fabriquer", "Fabriquer et rendre", "Rendre", "Posséder", "Tuer",
            "Fabriquer les objets", "Fabriquer et rendre les objets", "Rendre les objets pour la récompense",
            "Avoir les objets (non pris)", "Tuer les créatures", "Voir le libellé sous chaque objet",
            "Fabriqué : {0}/{1}",
            "AJOUTER UN ÉLÉMENT", "CHOISIR UN PRÉRÉGLAGE", "Objectifs_ID {0}", "Récompenses_ID {0}", "(nb)", "Texte d'information",
            "Direction | ", "Préréglage | ", "Nœud | ",
            "AJOUTER", "SUPPRIMER", "EFFACER", "SAUVEGARDER",
            "DÉPART", "QUÊTE", "CHECKPOINT", "TUER", "ÉDITEUR",
            "BRANCHES", "QUÊTES",
            "Branche : {0}", "Sélectionnez une branche ou créez-en une", "CHOISIR UNE BRANCHE", "Branche « {0} »",
            "+ BRANCHE", "Nouvelle branche de quêtes", "Nom de la branche", "Langue", "Entrez le nom", "En-tête : {0}",
            "CRÉER", "ANNULER", "MODIF.",
            "Cliquer pour choisir", "Cliquer pour choisir", "Choisir une icône", "FERMER",
            "SUPPRIMER", "Modifier la branche", "SAUVEGARDER", "Supprimer la branche ?", "Supprimer la branche « {0} » ? Irréversible.", "SUPPRIMER",
            "FERMER",
            "SÉLECT.", "NOUVEAU", "LIGNE", "SUPPRIMER", "GRILLE",
            "SÉLECT.", "NOUVELLE QUÊTE", "LIEN", "SUPPRIMER", "SAUVER", "RESET", "MODIF.",
            "GRILLE", "QUITTER", "BRANCHES", "QUÊTES", "CRÉER", "MODIF.", "SUPPR. BRANCHE",
            "Icône", "Éditeur", "DÉPART", "QUÊTE", "CHECKP.", "TUER",
            "Glisser les quêtes pour déplacer", "Sélectionné #{0}", "Cliquer une quête pour éditer", "Cliquer le graphe pour placer",
            "Lien : nœud de départ", "Lien : nœud d'arrivée", "Cliquer un nœud pour supprimer", "Sélectionner ou créer un nœud",
            "Édition #{0}", "Quête #{0}", "Choisir un outil",
            "Sélection d'une quête", "Quête #{0}", "Objectifs", "Tuer", "Récompenses", "Vide — appuyez sur +",
            "Choisir", "Choisir un objet", "Annuler", "Rechercher des objets…", "Aucun résultat",
            "Tous types", "Exact", "Prendre les objets (off = détecter seulement)", "Prendre à la validation", "Détecter seulement",
            "Fabriquer", "Posséder", "icônes : craft · tous types  |  prendre — en haut",
            "Consume", "Craft", "Tous types", "Tuer",
            "Cliquer pour choisir une créature", "Choisir une créature à tuer", "Rechercher des créatures…", "Aucun résultat",
            "Qté", "SAUVEGARDER");
        Categories(d,
            "Légende", "CHEMIN EMBERSTAR",
            "Âge de pierre", "ÂGE DE PIERRE", "Poterie", "POTERIE",
            "Âge du cuivre", "ÂGE DU CUIVRE", "Âge du bronze", "ÂGE DU BRONZE",
            "Âge du fer", "ÂGE DU FER", "Âge de l'acier", "ÂGE DE L'ACIER",
            "Chasse", "CHASSE", "Agriculture", "AGRICULTURE",
            "Artisanat", "ARTISANAT", "Pérégrinations", "PÉRÉGRINATIONS",
            "Branche {0}", "BRANCHE {0}");
        return d;
    }

    static Dictionary<string, string> Es()
    {
        var d = Pack(
            "MISIONES", "CERRAR", "Esta rama aún no tiene misiones.", "Diario de misiones",
            "MISIÓN: ", "COMPLETADA", "Objetivos:", "Recompensas:", "Texto de información",
            "INICIAR RUTA", "RUTA INICIADA", "PROCESANDO...", "SUPERADA", "CONTINUAR", "RECIBIDO", "RECOGER RECOMPENSA",
            "Fabricar", "Fabricar y entregar", "Entregar", "Obtener", "Matar",
            "Fabrica los objetos", "Fabrica y entrega los objetos", "Entrega los objetos por la recompensa",
            "Ten los objetos (no se quitan)", "Mata a las criaturas", "Mira la etiqueta de cada objeto",
            "Fabricado: {0}/{1}",
            "AÑADIR ELEMENTO", "ELEGIR PREAJUSTE", "Objetivos_ID {0}", "Recompensas_ID {0}", "(núm.)", "Texto de información",
            "Dirección | ", "Preajuste | ", "Nodo | ",
            "AÑADIR", "ELIMINAR", "LIMPIAR", "GUARDAR",
            "INICIO", "MISIÓN", "CHECKPOINT", "MATAR", "EDITOR",
            "RAMAS", "MISIONES",
            "Rama: {0}", "Selecciona una rama o crea una nueva", "ELEGIR RAMA", "Rama \"{0}\"",
            "+ RAMA", "Nueva rama de misiones", "Nombre de la rama", "Idioma", "Introduce el nombre", "Encabezado: {0}",
            "CREAR", "CANCELAR", "EDITAR",
            "Clic para elegir", "Clic para elegir", "Elegir icono", "CERRAR",
            "ELIMINAR", "Editar rama", "GUARDAR", "¿Eliminar rama?", "¿Eliminar la rama \"{0}\"? No se puede deshacer.", "ELIMINAR",
            "CERRAR",
            "SELECC.", "NUEVO", "LÍNEA", "ELIMINAR", "CUADRÍC.",
            "SELECC.", "NUEVA MISIÓN", "ENLACE", "ELIMINAR", "GUARDAR", "RESET", "EDITAR",
            "CUADRÍC.", "SALIR", "RAMAS", "MISIONES", "CREAR", "EDITAR", "ELIMINAR RAMA",
            "Icono", "Editor", "INICIO", "MISIÓN", "CHECKP.", "MATAR",
            "Arrastra misiones para mover", "Seleccionado #{0}", "Clic en una misión para editar", "Clic en el grafo para colocar",
            "Enlace: nodo inicial", "Enlace: nodo final", "Clic en un nodo para borrar", "Selecciona o crea un nodo",
            "Editando #{0}", "Misión #{0}", "Elige una herramienta",
            "Selección de misión", "Misión #{0}", "Objetivos", "Matar", "Recompensas", "Vacío — pulsa +",
            "Elegir", "Elegir un objeto", "Cancelar", "Buscar objetos…", "Sin resultados",
            "Todos los tipos", "Exacto", "Consumir objetos (off = solo detectar)", "Consumir al reclamar", "Solo detectar",
            "Fabricar", "Tener", "iconos: craft · todos  |  consumir — arriba",
            "Consume", "Craft", "Todos", "Matar",
            "Clic para elegir una criatura", "Elige una criatura para matar", "Buscar criaturas…", "Sin resultados",
            "Cant.", "GUARDAR");
        Categories(d,
            "Leyenda", "CAMINO EMBERSTAR",
            "Edad de piedra", "EDAD DE PIEDRA", "Alfarería", "ALFARERÍA",
            "Edad del cobre", "EDAD DEL COBRE", "Edad del bronce", "EDAD DEL BRONCE",
            "Edad del hierro", "EDAD DEL HIERRO", "Edad del acero", "EDAD DEL ACERO",
            "Caza", "CAZA", "Agricultura", "AGRICULTURA",
            "Oficios", "OFICIOS", "Andanzas", "ANDANZAS",
            "Rama {0}", "RAMA {0}");
        return d;
    }

    static Dictionary<string, string> EsLatam()
    {
        // Same as es-es with minor wording
        var d = Es();
        d["hotkey_name"] = "Diario de misiones";
        d["modal.claim_reward"] = "RECLAMAR RECOMPENSA";
        d["empty_category"] = "Esta rama todavía no tiene misiones.";
        return d;
    }

    static Dictionary<string, string> Pl()
    {
        var d = Pack(
            "QUESTY", "ZAMKNIJ", "Ta gałąź nie ma jeszcze questów.", "Dziennik questów",
            "QUEST: ", "UKOŃCZONY", "Cele:", "Nagrody:", "Tekst informacyjny",
            "ROZPOCZNIJ ŚCIEŻKĘ", "ŚCIEŻKA ROZPOCZĘTA", "PRZETWARZANIE...", "ZALICZONE", "KONTYNUUJ", "OTRZYMANO", "ODBIERZ NAGRODĘ",
            "Wytwórz", "Wytwórz i oddaj", "Oddaj", "Posiadaj", "Zabij",
            "Wytwórz przedmioty", "Wytwórz i oddaj przedmioty", "Oddaj przedmioty za nagrodę",
            "Miej przedmioty (nie są zabierane)", "Zabij stwory", "Zobacz opis pod każdym przedmiotem",
            "Wytworzono: {0}/{1}",
            "DODAJ ELEMENT", "WYBIERZ PRESET", "Cele_ID {0}", "Nagrody_ID {0}", "(ile)", "Tekst informacyjny",
            "Kierunek | ", "Preset | ", "Węzeł | ",
            "DODAJ", "USUŃ", "WYCZYŚĆ", "ZAPISZ",
            "START", "QUEST", "CHECKPOINT", "ZABIJ", "EDYTOR",
            "GAŁĘZIE", "QUESTY",
            "Gałąź: {0}", "Wybierz gałąź lub utwórz nową", "WYBIERZ GAŁĄŹ", "Gałąź \"{0}\"",
            "+ GAŁĄŹ", "Nowa gałąź questów", "Nazwa gałęzi", "Język", "Wpisz nazwę gałęzi", "Nagłówek: {0}",
            "UTWÓRZ", "ANULUJ", "EDYTUJ",
            "Kliknij, aby wybrać", "Kliknij, aby wybrać", "Wybierz ikonę", "ZAMKNIJ",
            "USUŃ", "Edytuj gałąź", "ZAPISZ", "Usunąć gałąź?", "Usunąć gałąź \"{0}\"? Tej operacji nie cofniesz.", "USUŃ",
            "ZAMKNIJ",
            "WYBÓR", "NOWY", "LINIA", "USUŃ", "SIATKA",
            "WYBÓR", "NOWY QUEST", "ŁĄCZ", "USUŃ", "ZAPISZ", "RESET", "EDYTUJ",
            "SIATKA", "WYJŚCIE", "GAŁĘZIE", "QUESTY", "UTWÓRZ", "EDYTUJ", "USUŃ GAŁĄŹ",
            "Ikona", "Edytor", "START", "QUEST", "CHECKP.", "ZABIJ",
            "Przeciągnij questy, aby przenieść", "Wybrano #{0}", "Kliknij quest, aby edytować", "Kliknij graf, aby umieścić",
            "Połączenie: węzeł startowy", "Połączenie: węzeł końcowy", "Kliknij węzeł, aby usunąć", "Wybierz lub utwórz węzeł",
            "Edycja #{0}", "Quest #{0}", "Wybierz narzędzie",
            "Wybór questa", "Quest #{0}", "Cele", "Zabij", "Nagrody", "Pusto — naciśnij +",
            "Wybierz", "Wybierz przedmiot", "Anuluj", "Szukaj przedmiotów…", "Brak wyników",
            "Wszystkie typy", "Dokładnie", "Zabieraj przedmioty (wył. = tylko wykrycie)", "Zabierz przy odbiorze", "Tylko wykrycie",
            "Craft", "Posiadaj", "ikony: craft · wszystkie  |  zabierz — u góry",
            "Consume", "Craft", "Wszystkie", "Zabij",
            "Kliknij, aby wybrać stworzenie", "Wybierz stworzenie do zabicia", "Szukaj stworzeń…", "Brak wyników",
            "Ilość", "ZAPISZ");
        Categories(d,
            "Legenda", "ŚCIEŻKA EMBERSTAR",
            "Epoka kamienia", "EPOKA KAMIENIA", "Garncarstwo", "GARNCARSTWO",
            "Epoka miedzi", "EPOKA MIEDZI", "Epoka brązu", "EPOKA BRĄZU",
            "Epoka żelaza", "EPOKA ŻELAZA", "Epoka stali", "EPOKA STALI",
            "Łowiectwo", "ŁOWIECTWO", "Rolnictwo", "ROLNICTWO",
            "Rzemiosło", "RZEMIOSŁO", "Wędrówki", "WĘDRÓWKI",
            "Gałąź {0}", "GAŁĄŹ {0}");
        return d;
    }

    static Dictionary<string, string> Uk()
    {
        var d = Pack(
            "КВЕСТИ", "ЗАКРИТИ", "У цій гілці ще немає квестів.", "Щоденник квестів",
            "КВЕСТ: ", "ЗАВЕРШЕНО", "Цілі:", "Нагороди:", "Інформаційний текст",
            "ПОЧАТИ ШЛЯХ", "ШЛЯХ ПОЧАТО", "ОБРОБКА...", "ПРОЙДЕНО", "ПРОДОВЖИТИ", "ОТРИМАНО", "ЗАБРАТИ НАГОРОДУ",
            "Скрафтити", "Скрафтити і здати", "Здати", "Отримати", "Вбити",
            "Потрібно скрафтити предмети", "Потрібно скрафтити і здати", "Потрібно здати предмети за нагороду",
            "Потрібно мати предмети (не забираються)", "Потрібно вбити істот", "Дивись підпис під кожним предметом",
            "Скрафчено: {0}/{1}",
            "ДОДАТИ ЕЛЕМЕНТ", "ОБРАТИ ПРЕСЕТ", "Цілі_ID {0}", "Нагороди_ID {0}", "(к-сть)", "Інформаційний текст",
            "Напрям | ", "Пресет | ", "Вузол | ",
            "ДОДАТИ", "ВИДАЛИТИ", "ОЧИСТИТИ", "ЗБЕРЕГТИ",
            "СТАРТ", "КВЕСТ", "ЧЕКПОІНТ", "ВБИТИ", "РЕДАКТОР",
            "ГІЛКИ", "КВЕСТИ",
            "Гілка: {0}", "Оберіть гілку або створіть нову", "ОБЕРІТЬ ГІЛКУ", "Гілка «{0}»",
            "+ ГІЛКА", "Нова гілка квестів", "Назва гілки", "Мова", "Введіть назву гілки", "Заголовок: {0}",
            "СТВОРИТИ", "СКАСУВАТИ", "ЗМІН.",
            "Клацніть, щоб обрати", "Клацніть, щоб обрати", "Вибір іконки", "ЗАКРИТИ",
            "ВИДАЛИТИ", "Редагувати гілку", "ЗБЕРЕГТИ", "Видалити гілку?", "Видалити гілку «{0}»? Цю дію не скасувати.", "ВИДАЛИТИ",
            "ЗАКРИТИ",
            "ВИБІР", "НОВИЙ", "ЛІНІЯ", "ВИДАЛИТИ", "СІТКА",
            "ВИБІР", "НОВИЙ КВЕСТ", "ЗВ'ЯЗОК", "ВИДАЛИТИ", "ЗБЕРЕГТИ", "СКИД.", "ЗМІНИТИ",
            "СІТКА", "ВИХІД", "ГІЛКИ", "КВЕСТИ", "СТВОРИТИ", "ЗМІНИТИ", "ВИДАЛИТИ ГІЛКУ",
            "Іконка", "Редактор", "СТАРТ", "КВЕСТ", "ЧЕКП.", "ВБИТИ",
            "Тягніть квест мишею", "Обрано #{0}", "Клацніть квест — змінити", "Клацніть граф — новий",
            "Зв'язок: вузол-початок", "Зв'язок: вузол-кінець", "Клацніть вузол — видалити", "Оберіть або створіть",
            "Квест #{0}", "Квест #{0}", "Оберіть інструмент",
            "Вибір квеста", "Квест #{0}", "Цілі", "Вбити", "Нагороди", "Порожньо — натисніть +",
            "Обрати", "Оберіть предмет", "Скасувати", "Пошук предметів…", "Нічого не знайдено",
            "Усі типи", "Точно", "Забирати предмети (вимк. = лише перевірка)", "Забирати при здачі", "Лише перевірка",
            "Крафт", "Мати", "іконки: крафт · усі типи  |  забрати — зверху",
            "Забрати", "Крафт", "Усі типи", "Вбити",
            "Клацніть, щоб обрати істоту", "Оберіть істоту для вбивства", "Пошук істот…", "Нічого не знайдено",
            "К-сть", "ЗБЕРЕГТИ");
        Categories(d,
            "Легенда", "ШЛЯХ EMBERSTAR",
            "Кам'яна доба", "КАМ'ЯНА ДОБА", "Гончарство", "ГОНЧАРСТВО",
            "Мідна доба", "МІДНА ДОБА", "Бронзова доба", "БРОНЗОВА ДОБА",
            "Залізна доба", "ЗАЛІЗНА ДОБА", "Сталева доба", "СТАЛЕВА ДОБА",
            "Полювання", "ПОЛЮВАННЯ", "Фермерство", "ФЕРМЕРСТВО",
            "Ремесла", "РЕМЕСЛА", "Мандри", "МАНДРИ",
            "Гілка {0}", "ГІЛКА {0}");
        return d;
    }

    static Dictionary<string, string> Be()
    {
        // Belarusian — close to Russian/Ukrainian admin UI
        var d = Pack(
            "КВЭСТЫ", "ЗАКРЫЦЬ", "У гэтай галіне пакуль няма квэстаў.", "Дзённік квэстаў",
            "КВЭСТ: ", "ЗАВЕРШАНЫ", "Мэты:", "Узнагароды:", "Інфармацыйны тэкст",
            "ПАЧАЦЬ ШЛЯХ", "ШЛЯХ ПАЧАТЫ", "АПРАЦОЎКА...", "ПРОЙДЗЕНА", "ПРАЦЯГНУЦЬ", "АТРЫМАНА", "ЗАБРАЦЬ УЗНАГАРОДУ",
            "Скрафціць", "Скрафціць і здаць", "Здаць", "Атрымаць", "Забіць",
            "Трэба скрафціць прадметы", "Трэба скрафціць і здаць", "Трэба здаць прадметы за ўзнагароду",
            "Трэба мець прадметы (не забіраюцца)", "Трэба забіць істот", "Глядзі подпіс пад кожным прадметам",
            "Скрафчана: {0}/{1}",
            "ДАДАЦЬ ЭЛЕМЕНТ", "ВЫБРАЦЬ ПРЭСЕТ", "Мэты_ID {0}", "Узнагароды_ID {0}", "(кольк.)", "Інфармацыйны тэкст",
            "Кірунак | ", "Прэсет | ", "Вузел | ",
            "ДАДАЦЬ", "ВЫДАЛІЦЬ", "АЧЫСЦІЦЬ", "ЗАХАВАЦЬ",
            "СТАРТ", "КВЭСТ", "ЧЭКПОІНТ", "ЗАБІЦЬ", "РЭДАКТАР",
            "Галіны", "КВЭСТЫ",
            "Галіна: {0}", "Выберыце галіну або стварыце новую", "ВЫБЕРЫЦЕ ГАЛІНУ", "Галіна «{0}»",
            "+ ГАЛІНА", "Новая галіна квэстаў", "Назва галіны", "Мова", "Увядзіце назву", "Загаловак: {0}",
            "СТВАРЫЦЬ", "СКАСАВАЦЬ", "ЗМЯН.",
            "Клікніце, каб выбраць", "Клікніце, каб выбраць", "Выбар іконкі", "ЗАКРЫЦЬ",
            "ВЫДАЛІЦЬ", "Рэдагаваць галіну", "ЗАХАВАЦЬ", "Выдаліць галіну?", "Выдаліць галіну «{0}»? Дзеянне нельга адмяніць.", "ВЫДАЛІЦЬ",
            "ЗАКРЫЦЬ",
            "ВЫБАР", "НОВЫ", "ЛІНІЯ", "ВЫДАЛІЦЬ", "СЕТКА",
            "ВЫБАР", "НОВЫ КВЭСТ", "СУВЯЗЬ", "ВЫДАЛІЦЬ", "ЗАХАВАЦЬ", "СКИД", "ЗМЯНІЦЬ",
            "СЕТКА", "ВЫХАД", "ГАЛІНЫ", "КВЭСТЫ", "СТВАРЫЦЬ", "ЗМЯНІЦЬ", "ВЫДАЛІЦЬ ГАЛІНУ",
            "Іконка", "Рэдактар", "СТАРТ", "КВЭСТ", "ЧЭКП.", "ЗАБІЦЬ",
            "Цягніце квэст мышкай", "Выбрана #{0}", "Клікніце квэст — змяніць", "Клікніце граф — новы",
            "Сувязь: вузел-пачатак", "Сувязь: вузел-канец", "Клікніце вузел — выдаліць", "Выберыце або стварыце",
            "Квэст #{0}", "Квэст #{0}", "Выберыце інструмент",
            "Выбар квэста", "Квэст #{0}", "Мэты", "Забіць", "Узнагароды", "Пуста — націсніце +",
            "Выбраць", "Выберыце прадмет", "Скасаваць", "Пошук прадметаў…", "Нічога не знойдзена",
            "Усе тыпы", "Дакладна", "Забіраць прадметы (выкл. = толькі праверка)", "Забіраць пры здачы", "Толькі праверка",
            "Крафт", "Мець", "іконкі: крафт · усе тыпы  |  забраць — зверху",
            "Забраць", "Крафт", "Усе тыпы", "Забіць",
            "Клікніце, каб выбраць істоту", "Выберыце істоту для забойства", "Пошук істот…", "Нічога не знойдзена",
            "Кольк.", "ЗАХАВАЦЬ");
        Categories(d,
            "Легенда", "ШЛЯХ EMBERSTAR",
            "Каменны век", "КАМЕННЫ ВЕК", "Ганчарства", "ГАНЧАРСТВА",
            "Медны век", "МЕДНЫ ВЕК", "Бронзавы век", "БРОНЗАВЫ ВЕК",
            "Жалезны век", "ЖАЛЕЗНЫ ВЕК", "Сталёвы век", "СТАЛЁВЫ ВЕК",
            "Паляванне", "ПАЛЯВАННЕ", "Фермерства", "ФЕРМЕРСТВА",
            "Рамёствы", "РАМЁСТВЫ", "Вандраванні", "ВАНДРАВАННІ",
            "Галіна {0}", "ГАЛІНА {0}");
        return d;
    }

    static Dictionary<string, string> PtBr()
    {
        var d = Pack(
            "MISSÕES", "FECHAR", "Este ramo ainda não tem missões.", "Diário de missões",
            "MISSÃO: ", "CONCLUÍDA", "Objetivos:", "Recompensas:", "Texto de informação",
            "INICIAR CAMINHO", "CAMINHO INICIADO", "PROCESSANDO...", "CONCLUÍDO", "CONTINUAR", "RECEBIDO", "COLETAR RECOMPENSA",
            "Criar", "Criar e entregar", "Entregar", "Obter", "Matar",
            "Crie os itens", "Crie e entregue os itens", "Entregue os itens pela recompensa",
            "Tenha os itens (não são removidos)", "Mate as criaturas", "Veja o rótulo de cada item",
            "Criado: {0}/{1}",
            "ADICIONAR ITEM", "SELECIONAR PRESET", "Objetivos_ID {0}", "Recompensas_ID {0}", "(núm.)", "Texto de informação",
            "Direção | ", "Preset | ", "Nó | ",
            "ADD", "EXCLUIR", "LIMPAR", "SALVAR",
            "INÍCIO", "MISSÃO", "CHECKPOINT", "MATAR", "EDITOR",
            "RAMOS", "MISSÕES",
            "Ramo: {0}", "Selecione um ramo ou crie um novo", "SELECIONAR RAMO", "Ramo \"{0}\"",
            "+ RAMO", "Novo ramo de missões", "Nome do ramo", "Idioma", "Digite o nome", "Cabeçalho: {0}",
            "CRIAR", "CANCELAR", "EDITAR",
            "Clique para escolher", "Clique para escolher", "Escolher ícone", "FECHAR",
            "EXCLUIR", "Editar ramo", "SALVAR", "Excluir ramo?", "Excluir o ramo \"{0}\"? Isso não pode ser desfeito.", "EXCLUIR",
            "FECHAR",
            "SELEC.", "NOVO", "LINHA", "EXCLUIR", "GRADE",
            "SELEC.", "NOVA MISSÃO", "LIGAR", "EXCLUIR", "SALVAR", "RESET", "EDITAR",
            "GRADE", "SAIR", "RAMOS", "MISSÕES", "CRIAR", "EDITAR", "EXCLUIR RAMO",
            "Ícone", "Editor", "INÍCIO", "MISSÃO", "CHECKP.", "MATAR",
            "Arraste missões para mover", "Selecionado #{0}", "Clique em uma missão para editar", "Clique no grafo para colocar",
            "Ligação: nó inicial", "Ligação: nó final", "Clique em um nó para excluir", "Selecione ou crie um nó",
            "Editando #{0}", "Missão #{0}", "Selecione uma ferramenta",
            "Seleção de missão", "Missão #{0}", "Objetivos", "Matar", "Recompensas", "Vazio — pressione +",
            "Escolher", "Escolher um item", "Cancelar", "Pesquisar itens…", "Nenhum resultado",
            "Todos os tipos", "Exato", "Consumir itens (off = só detectar)", "Consumir ao reivindicar", "Só detectar",
            "Criar", "Ter", "ícones: craft · todos  |  consumir — acima",
            "Consume", "Craft", "Todos", "Matar",
            "Clique para escolher uma criatura", "Escolha uma criatura para matar", "Pesquisar criaturas…", "Nenhum resultado",
            "Qtd", "SALVAR");
        Categories(d,
            "Lenda", "CAMINHO EMBERSTAR",
            "Idade da Pedra", "IDADE DA PEDRA", "Cerâmica", "CERÂMICA",
            "Idade do Cobre", "IDADE DO COBRE", "Idade do Bronze", "IDADE DO BRONZE",
            "Idade do Ferro", "IDADE DO FERRO", "Idade do Aço", "IDADE DO AÇO",
            "Caça", "CAÇA", "Agricultura", "AGRICULTURA",
            "Ofícios", "OFÍCIOS", "Andanças", "ANDANÇAS",
            "Ramo {0}", "RAMO {0}");
        return d;
    }

    static Dictionary<string, string> PtPt()
    {
        var d = PtBr();
        d["hotkey_name"] = "Diário de missões";
        d["modal.claim_reward"] = "RECOLHER RECOMPENSA";
        d["admin.save"] = "GUARDAR";
        d["admin.icon.save"] = "GUARDAR";
        d["admin.rename_branch.save"] = "GUARDAR";
        d["admin.quest_edit.save"] = "GUARDAR";
        d["admin.delete"] = "ELIMINAR";
        d["admin.icon.delete"] = "ELIMINAR";
        return d;
    }

    static Dictionary<string, string> It()
    {
        var d = Pack(
            "QUEST", "CHIUDI", "Questo ramo non ha ancora quest.", "Diario delle quest",
            "QUEST: ", "COMPLETATA", "Obiettivi:", "Ricompense:", "Testo informativo",
            "INIZIA PERCORSO", "PERCORSO INIZIATO", "ELABORAZIONE...", "SUPERATA", "CONTINUA", "RICEVUTO", "RISCUOTI RICOMPENSA",
            "Crea", "Crea e consegna", "Consegna", "Ottieni", "Uccidi",
            "Crea gli oggetti", "Crea e consegna gli oggetti", "Consegna gli oggetti per la ricompensa",
            "Possiedi gli oggetti (non vengono tolti)", "Uccidi le creature", "Vedi l'etichetta sotto ogni oggetto",
            "Creato: {0}/{1}",
            "AGGIUNGI ELEMENTO", "SELEZIONA PRESET", "Obiettivi_ID {0}", "Ricompense_ID {0}", "(n.)", "Testo informativo",
            "Direzione | ", "Preset | ", "Nodo | ",
            "AGGIUNGI", "ELIMINA", "PULISCI", "SALVA",
            "INIZIO", "QUEST", "CHECKPOINT", "UCCIDI", "EDITOR",
            "RAMI", "QUEST",
            "Ramo: {0}", "Seleziona un ramo o creane uno", "SELEZIONA RAMO", "Ramo \"{0}\"",
            "+ RAMO", "Nuovo ramo di quest", "Nome del ramo", "Lingua", "Inserisci il nome", "Intestazione: {0}",
            "CREA", "ANNULLA", "MODIFICA",
            "Clic per scegliere", "Clic per scegliere", "Scegli icona", "CHIUDI",
            "ELIMINA", "Modifica ramo", "SALVA", "Eliminare il ramo?", "Eliminare il ramo \"{0}\"? Non si può annullare.", "ELIMINA",
            "CHIUDI",
            "SELEZ.", "NUOVO", "LINEA", "ELIMINA", "GRIGLIA",
            "SELEZ.", "NUOVA QUEST", "COLLEGA", "ELIMINA", "SALVA", "RESET", "MODIFICA",
            "GRIGLIA", "ESCI", "RAMI", "QUEST", "CREA", "MODIFICA", "ELIMINA RAMO",
            "Icona", "Editor", "INIZIO", "QUEST", "CHECKP.", "UCCIDI",
            "Trascina le quest per spostare", "Selezionato #{0}", "Clic su una quest per modificare", "Clic sul grafo per piazzare",
            "Collegamento: nodo iniziale", "Collegamento: nodo finale", "Clic su un nodo per eliminare", "Seleziona o crea un nodo",
            "Modifica #{0}", "Quest #{0}", "Seleziona uno strumento",
            "Selezione quest", "Quest #{0}", "Obiettivi", "Uccidi", "Ricompense", "Vuoto — premi +",
            "Scegli", "Scegli un oggetto", "Annulla", "Cerca oggetti…", "Nessun risultato",
            "Tutti i tipi", "Esatto", "Consuma oggetti (off = solo rileva)", "Consuma al ritiro", "Solo rileva",
            "Crea", "Possiedi", "icone: craft · tutti  |  consuma — in alto",
            "Consume", "Craft", "Tutti", "Uccidi",
            "Clic per scegliere una creatura", "Scegli una creatura da uccidere", "Cerca creature…", "Nessun risultato",
            "Q.tà", "SALVA");
        Categories(d,
            "Leggenda", "SENTIERO EMBERSTAR",
            "Età della pietra", "ETÀ DELLA PIETRA", "Ceramica", "CERAMICA",
            "Età del rame", "ETÀ DEL RAME", "Età del bronzo", "ETÀ DEL BRONZO",
            "Età del ferro", "ETÀ DEL FERRO", "Età dell'acciaio", "ETÀ DELL'ACCIAIO",
            "Caccia", "CACCIA", "Agricoltura", "AGRICOLTURA",
            "Mestieri", "MESTIERI", "Peregrinazioni", "PEREGRINAZIONI",
            "Ramo {0}", "RAMO {0}");
        return d;
    }

    static Dictionary<string, string> Cs()
    {
        var d = Pack(
            "ÚKOLY", "ZAVŘÍT", "Tato větev zatím nemá žádné úkoly.", "Deník úkolů",
            "ÚKOL: ", "DOKONČENO", "Cíle:", "Odměny:", "Informační text",
            "ZAČÍT CESTU", "CESTA ZAHÁJENA", "ZPRACOVÁNÍ...", "SPLNĚNO", "POKRAČOVAT", "ZÍSKÁNO", "VYBRAT ODMĚNU",
            "Vyrobit", "Vyrobit a odevzdat", "Odevzdat", "Získat", "Zabít",
            "Vyrobte předměty", "Vyrobte a odevzdejte předměty", "Odevzdejte předměty za odměnu",
            "Mějte předměty (nejsou odebrány)", "Zabijte tvory", "Viz popisek u každého předmětu",
            "Vyrobeno: {0}/{1}",
            "PŘIDAT POLOŽKU", "VYBRAT PŘEDVOLBU", "Cíle_ID {0}", "Odměny_ID {0}", "(počet)", "Informační text",
            "Směr | ", "Předvolba | ", "Uzel | ",
            "PŘIDAT", "SMAZAT", "VYMAZAT", "ULOŽIT",
            "START", "ÚKOL", "CHECKPOINT", "ZABÍT", "EDITOR",
            "VĚTVE", "ÚKOLY",
            "Větev: {0}", "Vyberte větev nebo vytvořte novou", "VYBERTE VĚTEV", "Větev \"{0}\"",
            "+ VĚTEV", "Nová větev úkolů", "Název větve", "Jazyk", "Zadejte název", "Záhlaví: {0}",
            "VYTVOŘIT", "ZRUŠIT", "UPRAVIT",
            "Klikněte pro výběr", "Klikněte pro výběr", "Vybrat ikonu", "ZAVŘÍT",
            "SMAZAT", "Upravit větev", "ULOŽIT", "Smazat větev?", "Smazat větev \"{0}\"? Tuto akci nelze vrátit.", "SMAZAT",
            "ZAVŘÍT",
            "VÝBĚR", "NOVÝ", "ČÁRA", "SMAZAT", "MŘÍŽKA",
            "VÝBĚR", "NOVÝ ÚKOL", "VAZBA", "SMAZAT", "ULOŽIT", "RESET", "UPRAVIT",
            "MŘÍŽKA", "KONEC", "VĚTVE", "ÚKOLY", "VYTVOŘIT", "UPRAVIT", "SMAZAT VĚTEV",
            "Ikona", "Editor", "START", "ÚKOL", "CHECKP.", "ZABÍT",
            "Přetáhněte úkoly pro přesun", "Vybráno #{0}", "Klikněte na úkol pro úpravu", "Klikněte na graf pro umístění",
            "Vazba: počáteční uzel", "Vazba: koncový uzel", "Klikněte na uzel pro smazání", "Vyberte nebo vytvořte uzel",
            "Úprava #{0}", "Úkol #{0}", "Vyberte nástroj",
            "Výběr úkolu", "Úkol #{0}", "Cíle", "Zabít", "Odměny", "Prázdné — stiskněte +",
            "Vybrat", "Vyberte předmět", "Zrušit", "Hledat předměty…", "Nic nenalezeno",
            "Všechny typy", "Přesně", "Odebírat předměty (vyp. = jen detekce)", "Odebrat při převzetí", "Jen detekce",
            "Výroba", "Mít", "ikony: craft · všechny  |  odebrat — nahoře",
            "Consume", "Craft", "Všechny", "Zabít",
            "Klikněte pro výběr tvora", "Vyberte tvora k zabití", "Hledat tvory…", "Nic nenalezeno",
            "Počet", "ULOŽIT");
        Categories(d,
            "Legenda", "CESTA EMBERSTAR",
            "Doba kamenná", "DOBA KAMENNÁ", "Hrnčířství", "HRNČÍŘSTVÍ",
            "Doba měděná", "DOBA MĚDĚNÁ", "Doba bronzová", "DOBA BRONZOVÁ",
            "Doba železná", "DOBA ŽELEZNÁ", "Doba ocelová", "DOBA OCELOVÁ",
            "Lov", "LOV", "Zemědělství", "ZEMĚDĚLSTVÍ",
            "Řemesla", "ŘEMESLA", "Putování", "PUTOVÁNÍ",
            "Větev {0}", "VĚTEV {0}");
        return d;
    }

    static Dictionary<string, string> Sk()
    {
        var d = Pack(
            "ÚLOHY", "ZAVRIEŤ", "Táto vetva zatiaľ nemá žiadne úlohy.", "Denník úloh",
            "ÚLOHA: ", "DOKONČENÉ", "Ciele:", "Odmeny:", "Informačný text",
            "ZAČAŤ CESTU", "CESTA ZAČATÁ", "SPRACOVANIE...", "SPLNENÉ", "POKRAČOVAŤ", "ZÍSKANÉ", "VYBRAŤ ODMENU",
            "Vyrobiť", "Vyrobiť a odovzdať", "Odovzdať", "Získať", "Zabiť",
            "Vyrobte predmety", "Vyrobte a odovzdajte predmety", "Odovzdajte predmety za odmenu",
            "Majte predmety (neodoberajú sa)", "Zabite tvory", "Pozri popis pod každým predmetom",
            "Vyrobené: {0}/{1}",
            "PRIDAŤ POLOŽKU", "VYBRAŤ PREDVOĽBU", "Ciele_ID {0}", "Odmeny_ID {0}", "(počet)", "Informačný text",
            "Smer | ", "Predvoľba | ", "Uzol | ",
            "PRIDAŤ", "ZMAZAŤ", "VYMAZAŤ", "ULOŽIŤ",
            "ŠTART", "ÚLOHA", "CHECKPOINT", "ZABIŤ", "EDITOR",
            "VETVY", "ÚLOHY",
            "Vetva: {0}", "Vyberte vetvu alebo vytvorte novú", "VYBERTE VETVU", "Vetva \"{0}\"",
            "+ VETVA", "Nová vetva úloh", "Názov vetvy", "Jazyk", "Zadajte názov", "Hlavička: {0}",
            "VYTVORIŤ", "ZRUŠIŤ", "UPRAVIŤ",
            "Kliknite pre výber", "Kliknite pre výber", "Vybrať ikonu", "ZAVRIEŤ",
            "ZMAZAŤ", "Upraviť vetvu", "ULOŽIŤ", "Zmazať vetvu?", "Zmazať vetvu \"{0}\"? Túto akciu nemožno vrátiť.", "ZMAZAŤ",
            "ZAVRIEŤ",
            "VÝBER", "NOVÝ", "ČIARA", "ZMAZAŤ", "MRIEŽKA",
            "VÝBER", "NOVÁ ÚLOHA", "VÄZBA", "ZMAZAŤ", "ULOŽIŤ", "RESET", "UPRAVIŤ",
            "MRIEŽKA", "KONIEC", "VETVY", "ÚLOHY", "VYTVORIŤ", "UPRAVIŤ", "ZMAZAŤ VETVU",
            "Ikona", "Editor", "ŠTART", "ÚLOHA", "CHECKP.", "ZABIŤ",
            "Potiahnite úlohy na presun", "Vybrané #{0}", "Kliknite na úlohu pre úpravu", "Kliknite na graf pre umiestnenie",
            "Väzba: počiatočný uzol", "Väzba: koncový uzol", "Kliknite na uzol pre zmazanie", "Vyberte alebo vytvorte uzol",
            "Úprava #{0}", "Úloha #{0}", "Vyberte nástroj",
            "Výber úlohy", "Úloha #{0}", "Ciele", "Zabiť", "Odmeny", "Prázdne — stlačte +",
            "Vybrať", "Vyberte predmet", "Zrušiť", "Hľadať predmety…", "Nič nenájdené",
            "Všetky typy", "Presne", "Odoberať predmety (vyp. = len detekcia)", "Odobrať pri prevzatí", "Len detekcia",
            "Výroba", "Mať", "ikony: craft · všetky  |  odobrať — hore",
            "Consume", "Craft", "Všetky", "Zabiť",
            "Kliknite pre výber tvora", "Vyberte tvora na zabitie", "Hľadať tvory…", "Nič nenájdené",
            "Počet", "ULOŽIŤ");
        Categories(d,
            "Legenda", "CESTA EMBERSTAR",
            "Doba kamenná", "DOBA KAMENNÁ", "Hrnčiarstvo", "HRNČIARSTVO",
            "Doba medená", "DOBA MEDENÁ", "Doba bronzová", "DOBA BRONZOVÁ",
            "Doba železná", "DOBA ŽELEZNÁ", "Doba oceľová", "DOBA OCEĽOVÁ",
            "Lov", "LOV", "Poľnohospodárstvo", "POĽNOHOSPODÁRSTVO",
            "Remeslá", "REMESLÁ", "Putovanie", "PUTOVANIE",
            "Vetva {0}", "VETVA {0}");
        return d;
    }

    static Dictionary<string, string> Nl()
    {
        var d = Pack(
            "QUESTS", "SLUITEN", "Deze tak heeft nog geen quests.", "Questlogboek",
            "QUEST: ", "VOLTOOID", "Doelen:", "Beloningen:", "Informatietekst",
            "PAD STARTEN", "PAD GESTART", "VERWERKEN...", "GESLAAGD", "DOORGAAN", "ONTVANGEN", "BELOONING OPHALEN",
            "Craften", "Craften & inleveren", "Inleveren", "Verkrijgen", "Doden",
            "Craft de items", "Craft en lever items in", "Lever items in voor de beloning",
            "Heb de items (worden niet afgenomen)", "Dood de wezens", "Zie label onder elk item",
            "Gecraft: {0}/{1}",
            "ITEM TOEVOEGEN", "KIES PRESET", "Doelen_ID {0}", "Beloningen_ID {0}", "(aantal)", "Informatietekst",
            "Richting | ", "Preset | ", "Knoop | ",
            "TOEVOEGEN", "VERWIJDER", "WISSEN", "OPSLAAN",
            "START", "QUEST", "CHECKPOINT", "DODEN", "EDITOR",
            "TAKKEN", "QUESTS",
            "Tak: {0}", "Selecteer een tak of maak een nieuwe", "SELECTEER TAK", "Tak \"{0}\"",
            "+ TAK", "Nieuwe questtak", "Taknaam", "Taal", "Voer taknaam in", "Kop: {0}",
            "MAKEN", "ANNULEREN", "BEWERK",
            "Klik om te kiezen", "Klik om te kiezen", "Kies icoon", "SLUITEN",
            "VERWIJDER", "Tak bewerken", "OPSLAAN", "Tak verwijderen?", "Tak \"{0}\" verwijderen? Dit kan niet ongedaan.", "VERWIJDER",
            "SLUITEN",
            "SELECT.", "NIEUW", "LIJN", "VERWIJDER", "RASTER",
            "SELECT.", "NIEUWE QUEST", "KOPPEL", "VERWIJDER", "OPSLAAN", "RESET", "BEWERK",
            "RASTER", "EXIT", "TAKKEN", "QUESTS", "MAKEN", "BEWERK", "TAK VERWIJDEREN",
            "Icoon", "Editor", "START", "QUEST", "CHECKP.", "DODEN",
            "Sleep quests om te verplaatsen", "Geselecteerd #{0}", "Klik op een quest om te bewerken", "Klik op de grafiek om te plaatsen",
            "Koppel: startknoop", "Koppel: eindknoop", "Klik op een knoop om te verwijderen", "Selecteer of maak een knoop",
            "Bewerken #{0}", "Quest #{0}", "Selecteer een tool",
            "Quest selecteren", "Quest #{0}", "Doelen", "Doden", "Beloningen", "Leeg — druk op +",
            "Kies", "Kies een item", "Annuleren", "Items zoeken…", "Geen resultaten",
            "Alle types", "Exact", "Items afnemen (uit = alleen detecteren)", "Afnemen bij ophalen", "Alleen detecteren",
            "Craft", "Hebben", "iconen: craft · alle  |  afnemen — boven",
            "Consume", "Craft", "Alle types", "Doden",
            "Klik om een wezen te kiezen", "Kies een wezen om te doden", "Wezens zoeken…", "Geen resultaten",
            "Aantal", "OPSLAAN");
        Categories(d,
            "Legende", "EMBERSTAR-PAD",
            "Steentijd", "STEENTIJD", "Aardewerk", "AARDEWERK",
            "Koper tijd", "KOPERTIJD", "Bronstijd", "BRONSTIJD",
            "IJzertijd", "IJZERTIJD", "Staaltijd", "STAALTIJD",
            "Jacht", "JACHT", "Landbouw", "LANDBOUW",
            "Ambacht", "AMBACHT", "Zwerftochten", "ZWERFTOCHTEN",
            "Tak {0}", "TAK {0}");
        return d;
    }

    static Dictionary<string, string> Da()
    {
        var d = Pack(
            "QUESTS", "LUK", "Denne gren har ingen quests endnu.", "Questjournal",
            "QUEST: ", "FULDFØRT", "Mål:", "Belønninger:", "Informationstekst",
            "START STI", "STI STARTET", "BEHANDLER...", "BESTÅET", "FORTSÆT", "MODTAGET", "HENT BELØNNING",
            "Lav", "Lav & aflever", "Aflever", "Opnå", "Dræb",
            "Lav genstandene", "Lav og aflever genstande", "Aflever genstande for belønningen",
            "Hav genstandene (tages ikke)", "Dræb væsnerne", "Se etiketten under hver genstand",
            "Lavet: {0}/{1}",
            "TILFØJ ELEMENT", "VÆLG FORUDINDST.", "Mål_ID {0}", "Belønninger_ID {0}", "(antal)", "Informationstekst",
            "Retning | ", "Forudindst. | ", "Knude | ",
            "TILFØJ", "SLET", "RYD", "GEM",
            "START", "QUEST", "CHECKPOINT", "DRÆB", "EDITOR",
            "GRENE", "QUESTS",
            "Gren: {0}", "Vælg en gren eller opret en ny", "VÆLG GREN", "Gren \"{0}\"",
            "+ GREN", "Ny questgren", "Grennavn", "Sprog", "Indtast grennavn", "Overskrift: {0}",
            "OPRET", "ANNULLER", "REDIGER",
            "Klik for at vælge", "Klik for at vælge", "Vælg ikon", "LUK",
            "SLET", "Rediger gren", "GEM", "Slet gren?", "Slet gren \"{0}\"? Kan ikke fortrydes.", "SLET",
            "LUK",
            "VÆLG", "NY", "LINJE", "SLET", "GITTER",
            "VÆLG", "NY QUEST", "LINK", "SLET", "GEM", "NULSTIL", "REDIGER",
            "GITTER", "AFSLUT", "GRENE", "QUESTS", "OPRET", "REDIGER", "SLET GREN",
            "Ikon", "Editor", "START", "QUEST", "CHECKP.", "DRÆB",
            "Træk quests for at flytte", "Valgt #{0}", "Klik på en quest for at redigere", "Klik på grafen for at placere",
            "Link: startknude", "Link: slutknude", "Klik på en knude for at slette", "Vælg eller opret en knude",
            "Redigerer #{0}", "Quest #{0}", "Vælg et værktøj",
            "Vælg quest", "Quest #{0}", "Mål", "Dræb", "Belønninger", "Tom — tryk +",
            "Vælg", "Vælg en genstand", "Annuller", "Søg genstande…", "Ingen resultater",
            "Alle typer", "Præcis", "Tag genstande (fra = kun detekter)", "Tag ved indløsning", "Kun detekter",
            "Lav", "Hav", "ikoner: craft · alle  |  tag — øverst",
            "Consume", "Craft", "Alle typer", "Dræb",
            "Klik for at vælge et væsen", "Vælg et væsen at dræbe", "Søg væsner…", "Ingen resultater",
            "Antal", "GEM");
        Categories(d,
            "Legende", "EMBERSTAR-STI",
            "Stenalder", "STENALDER", "Keramic", "KERAMIK",
            "Kobberalder", "KOBBERALDER", "Bronzealder", "BRONZEALDER",
            "Jernalder", "JERNALDER", "Stålder", "STÅLDER",
            "Jagt", "JAGT", "Landbrug", "LANDBRUG",
            "Håndværk", "HÅNDVÆRK", "Vandringer", "VANDRINGER",
            "Gren {0}", "GREN {0}");
        return d;
    }

    static Dictionary<string, string> No()
    {
        var d = Pack(
            "QUESTS", "LUKK", "Denne grenen har ingen quests ennå.", "Questjournal",
            "QUEST: ", "FULLFØRT", "Mål:", "Belønninger:", "Informasjonstekst",
            "START STI", "STI STARTET", "BEHANDLER...", "BESTÅTT", "FORTSETT", "MOTTATT", "HENT BELØNNING",
            "Lag", "Lag & lever inn", "Lever inn", "Oppnå", "Drep",
            "Lag gjenstandene", "Lag og lever inn gjenstander", "Lever inn gjenstander for belønningen",
            "Ha gjenstandene (tas ikke)", "Drep skapningene", "Se etiketten under hver gjenstand",
            "Laget: {0}/{1}",
            "LEGG TIL ELEMENT", "VELG FORHÅNDSVALG", "Mål_ID {0}", "Belønninger_ID {0}", "(ant.)", "Informasjonstekst",
            "Retning | ", "Forhåndsvalg | ", "Node | ",
            "LEGG TIL", "SLETT", "TØM", "LAGRE",
            "START", "QUEST", "CHECKPOINT", "DREP", "EDITOR",
            "GRENER", "QUESTS",
            "Gren: {0}", "Velg en gren eller lag en ny", "VELG GREN", "Gren \"{0}\"",
            "+ GREN", "Ny questgren", "Grennavn", "Språk", "Skriv grennavn", "Overskrift: {0}",
            "OPPRETT", "AVBRYT", "REDIGER",
            "Klikk for å velge", "Klikk for å velge", "Velg ikon", "LUKK",
            "SLETT", "Rediger gren", "LAGRE", "Slette gren?", "Slette gren \"{0}\"? Kan ikke angres.", "SLETT",
            "LUKK",
            "VELG", "NY", "LINJE", "SLETT", "RUTENETT",
            "VELG", "NY QUEST", "KOBLE", "SLETT", "LAGRE", "NULLSTILL", "REDIGER",
            "RUTENETT", "AVSLUTT", "GRENER", "QUESTS", "OPPRETT", "REDIGER", "SLETT GREN",
            "Ikon", "Editor", "START", "QUEST", "CHECKP.", "DREP",
            "Dra quests for å flytte", "Valgt #{0}", "Klikk en quest for å redigere", "Klikk grafen for å plassere",
            "Kobling: startnode", "Kobling: sluttnode", "Klikk en node for å slette", "Velg eller opprett en node",
            "Redigerer #{0}", "Quest #{0}", "Velg et verktøy",
            "Velg quest", "Quest #{0}", "Mål", "Drep", "Belønninger", "Tom — trykk +",
            "Velg", "Velg en gjenstand", "Avbryt", "Søk gjenstander…", "Ingen treff",
            "Alle typer", "Eksakt", "Ta gjenstander (av = bare oppdag)", "Ta ved innløsning", "Bare oppdag",
            "Lag", "Ha", "ikoner: craft · alle  |  ta — øverst",
            "Consume", "Craft", "Alle typer", "Drep",
            "Klikk for å velge en skapning", "Velg en skapning å drepe", "Søk skapninger…", "Ingen treff",
            "Antall", "LAGRE");
        Categories(d,
            "Legende", "EMBERSTAR-STI",
            "Steinalder", "STEINALDER", "Keramikk", "KERAMIKK",
            "Kobberalder", "KOBBERALDER", "Bronsealder", "BRONSEALDER",
            "Jernalder", "JERNALDER", "Stålder", "STÅLDER",
            "Jakt", "JAKT", "Jordbruk", "JORDBRUK",
            "Håndverk", "HÅNDVERK", "Vandringer", "VANDRINGER",
            "Gren {0}", "GREN {0}");
        return d;
    }

    static Dictionary<string, string> Sv()
    {
        var d = Pack(
            "UPPDRAG", "STÄNG", "Denna gren har inga uppdrag ännu.", "Uppdragsjournal",
            "UPPDRAG: ", "SLUTFÖRD", "Mål:", "Belöningar:", "Informationstext",
            "STARTA VÄG", "VÄG STARTAD", "BEHANDLAR...", "GODKÄND", "FORTSÄTT", "MOTTAGEN", "HÄMTA BELÖNING",
            "Tillverka", "Tillverka & lämna in", "Lämna in", "Skaffa", "Döda",
            "Tillverka föremålen", "Tillverka och lämna in", "Lämna in föremål för belöningen",
            "Ha föremålen (tas inte)", "Döda varelserna", "Se etiketten under varje föremål",
            "Tillverkat: {0}/{1}",
            "LÄGG TILL ELEMENT", "VÄLJ FÖRINSTÄLLNING", "Mål_ID {0}", "Belöningar_ID {0}", "(antal)", "Informationstext",
            "Riktning | ", "Förinställning | ", "Nod | ",
            "LÄGG TILL", "TA BORT", "RENSA", "SPARA",
            "START", "UPPDRAG", "CHECKPOINT", "DÖDA", "REDAKTÖR",
            "GRENAR", "UPPDRAG",
            "Gren: {0}", "Välj en gren eller skapa en ny", "VÄLJ GREN", "Gren \"{0}\"",
            "+ GREN", "Ny uppdragsgren", "Grennamn", "Språk", "Ange grennamn", "Rubrik: {0}",
            "SKAPA", "AVBRYT", "REDIGERA",
            "Klicka för att välja", "Klicka för att välja", "Välj ikon", "STÄNG",
            "TA BORT", "Redigera gren", "SPARA", "Ta bort gren?", "Ta bort grenen \"{0}\"? Kan inte ångras.", "TA BORT",
            "STÄNG",
            "VÄLJ", "NY", "LINJE", "TA BORT", "RUTNÄT",
            "VÄLJ", "NYTT UPPDRAG", "LÄNK", "TA BORT", "SPARA", "NOLLSTÄLL", "REDIGERA",
            "RUTNÄT", "AVSLUTA", "GRENAR", "UPPDRAG", "SKAPA", "REDIGERA", "TA BORT GREN",
            "Ikon", "Redaktör", "START", "UPPDRAG", "CHECKP.", "DÖDA",
            "Dra uppdrag för att flytta", "Vald #{0}", "Klicka på ett uppdrag för att redigera", "Klicka på grafen för att placera",
            "Länk: startnod", "Länk: slutnod", "Klicka på en nod för att ta bort", "Välj eller skapa en nod",
            "Redigerar #{0}", "Uppdrag #{0}", "Välj ett verktyg",
            "Välj uppdrag", "Uppdrag #{0}", "Mål", "Döda", "Belöningar", "Tom — tryck +",
            "Välj", "Välj ett föremål", "Avbryt", "Sök föremål…", "Inga resultat",
            "Alla typer", "Exakt", "Ta föremål (av = bara detektera)", "Ta vid inlämning", "Bara detektera",
            "Tillverka", "Ha", "ikoner: craft · alla  |  ta — överst",
            "Consume", "Craft", "Alla typer", "Döda",
            "Klicka för att välja en varelse", "Välj en varelse att döda", "Sök varelser…", "Inga resultat",
            "Antal", "SPARA");
        Categories(d,
            "Legend", "EMBERSTAR-STIG",
            "Stenålder", "STENÅLDER", "Keramic", "KERAMIK",
            "Kopparålder", "KOPPARÅLDER", "Bronsålder", "BRONSÅLDER",
            "Järnålder", "JÄRNÅLDER", "Stålålder", "STÅLÅLDER",
            "Jakt", "JAKT", "Jordbruk", "JORDBRUK",
            "Hantverk", "HANTVERK", "Vandringar", "VANDRINGAR",
            "Gren {0}", "GREN {0}");
        return d;
    }

    static Dictionary<string, string> Fi()
    {
        var d = Pack(
            "TEHTÄVÄT", "SULJE", "Tällä haaralla ei ole vielä tehtäviä.", "Tehtäväpäiväkirja",
            "TEHTÄVÄ: ", "VALMIS", "Tavoitteet:", "Palkinnot:", "Infoteksti",
            "ALOITA POLKU", "POLKU ALOITETTU", "KÄSITELLÄÄN...", "LÄPÄISTY", "JATKA", "SAATU", "NOUDA PALKINTO",
            "Valmista", "Valmista ja luovuta", "Luovuta", "Hanki", "Tapa",
            "Valmista esineet", "Valmista ja luovuta esineet", "Luovuta esineet palkintoa varten",
            "Omista esineet (ei oteta)", "Tapa olennot", "Katso kunkin esineen selite",
            "Valmistettu: {0}/{1}",
            "LISÄÄ KOHDE", "VALITSE ESILASETUS", "Tavoitteet_ID {0}", "Palkinnot_ID {0}", "(kpl)", "Infoteksti",
            "Suunta | ", "Esilasetus | ", "Solmu | ",
            "LISÄÄ", "POISTA", "TYHJENNÄ", "TALLENNA",
            "ALKU", "TEHTÄVÄ", "TARKISTUSPISTE", "TAPA", "MUOKKAIN",
            "HAARAT", "TEHTÄVÄT",
            "Haara: {0}", "Valitse haara tai luo uusi", "VALITSE HAARA", "Haara \"{0}\"",
            "+ HAARA", "Uusi tehtävähaara", "Haaran nimi", "Kieli", "Anna haaran nimi", "Otsikko: {0}",
            "LUO", "PERUUTA", "MUOKKAA",
            "Napsauta valitaksesi", "Napsauta valitaksesi", "Valitse kuvake", "SULJE",
            "POISTA", "Muokkaa haaraa", "TALLENNA", "Poista haara?", "Poista haara \"{0}\"? Ei voi perua.", "POISTA",
            "SULJE",
            "VALITSE", "UUSI", "VIIVA", "POISTA", "RUUDUKKO",
            "VALITSE", "UUSI TEHTÄVÄ", "LINKKI", "POISTA", "TALLENNA", "NOLLAA", "MUOKKAA",
            "RUUDUKKO", "POISTU", "HAARAT", "TEHTÄVÄT", "LUO", "MUOKKAA", "POISTA HAARA",
            "Kuvake", "Muokkain", "ALKU", "TEHTÄVÄ", "TARK.", "TAPA",
            "Vedä tehtäviä siirtääksesi", "Valittu #{0}", "Napsauta tehtävää muokataksesi", "Napsauta kaaviota sijoittaaksesi",
            "Linkki: aloitussolmu", "Linkki: loppusolmu", "Napsauta solmua poistaaksesi", "Valitse tai luo solmu",
            "Muokataan #{0}", "Tehtävä #{0}", "Valitse työkalu",
            "Tehtävän valinta", "Tehtävä #{0}", "Tavoitteet", "Tapa", "Palkinnot", "Tyhjä — paina +",
            "Valitse", "Valitse esine", "Peruuta", "Etsi esineitä…", "Ei tuloksia",
            "Kaikki tyypit", "Tarkka", "Ota esineet (pois = vain tunnista)", "Ota lunastaessa", "Vain tunnista",
            "Valmista", "Omista", "kuvakkeet: craft · kaikki  |  ota — ylhäällä",
            "Consume", "Craft", "Kaikki", "Tapa",
            "Napsauta valitaksesi olennon", "Valitse tapettava olento", "Etsi olentoja…", "Ei tuloksia",
            "Määrä", "TALLENNA");
        Categories(d,
            "Legenda", "EMBERSTAR-POLKU",
            "Kivikausi", "KIVIKAUSI", "Keramiikka", "KERAMIIKKA",
            "Kuparikaausi", "KUPARIKAUSI", "Pronssikausi", "PRONSSIKAUSI",
            "Rautakausi", "RAUTAKAUSI", "Teräskausi", "TERÄSKAUSI",
            "Metsästys", "METSÄSTYS", "Maatalous", "MAATALOUS",
            "Käsityöt", "KÄSITYÖT", "Vaellukset", "VAELLUKSET",
            "Haara {0}", "HAARA {0}");
        return d;
    }

    static Dictionary<string, string> Hu()
    {
        var d = Pack(
            "KÜLDETÉSEK", "BEZÁRÁS", "Ehhez az ághoz még nincsenek küldetések.", "Küldetésnapló",
            "KÜLDETÉS: ", "KÉSZ", "Célok:", "Jutalmak:", "Információs szöveg",
            "ÚT INDÍTÁSA", "ÚT ELINDULT", "FELDOLGOZÁS...", "TELJESÍTVE", "TOVÁBB", "MEGKAPTAD", "JUTALOM FELVÉTELE",
            "Készítés", "Készítés és leadás", "Leadás", "Megszerzés", "Ölés",
            "Készítsd el a tárgyakat", "Készítsd el és add le", "Add le a tárgyakat a jutalomért",
            "Legyenek meg a tárgyak (nem veszik el)", "Öld meg a lényeket", "Lásd a címkét minden tárgynál",
            "Készítve: {0}/{1}",
            "ELEM HOZZÁADÁSA", "ELŐBEÁLL. VÁLASZTÁSA", "Célok_ID {0}", "Jutalmak_ID {0}", "(db)", "Információs szöveg",
            "Irány | ", "Előbeáll. | ", "Csomópont | ",
            "HOZZÁAD", "TÖRLÉS", "ÜRÍTÉS", "MENTÉS",
            "KEZDÉS", "KÜLDETÉS", "ELLENŐRZŐPONT", "ÖLÉS", "SZERKESZTŐ",
            "ÁGAK", "KÜLDETÉSEK",
            "Ág: {0}", "Válassz ágat vagy hozz létre újat", "ÁG VÁLASZTÁSA", "Ág \"{0}\"",
            "+ ÁG", "Új küldetéság", "Ág neve", "Nyelv", "Add meg az ág nevét", "Fejléc: {0}",
            "LÉTREHOZÁS", "MÉGSE", "SZERK.",
            "Kattints a választáshoz", "Kattints a választáshoz", "Ikon választása", "BEZÁRÁS",
            "TÖRLÉS", "Ág szerkesztése", "MENTÉS", "Ág törlése?", "Törlöd az ágat: \"{0}\"? Nem vonható vissza.", "TÖRLÉS",
            "BEZÁRÁS",
            "KIVÁLASZT", "ÚJ", "VONAL", "TÖRLÉS", "RÁCS",
            "KIVÁLASZT", "ÚJ KÜLDETÉS", "KAPCSOLAT", "TÖRLÉS", "MENTÉS", "RESET", "SZERK.",
            "RÁCS", "KILÉPÉS", "ÁGAK", "KÜLDETÉSEK", "LÉTREHOZ", "SZERK.", "ÁG TÖRLÉSE",
            "Ikon", "Szerkesztő", "KEZDÉS", "KÜLDETÉS", "PONT", "ÖLÉS",
            "Húzd a küldetéseket a mozgatáshoz", "Kiválasztva #{0}", "Kattints a küldetésre a szerkesztéshez", "Kattints a gráfra az elhelyezéshez",
            "Kapcsolat: kezdő csomópont", "Kapcsolat: záró csomópont", "Kattints a csomópontra a törléshez", "Válassz vagy hozz létre csomópontot",
            "Szerkesztés #{0}", "Küldetés #{0}", "Válassz eszközt",
            "Küldetés kiválasztása", "Küldetés #{0}", "Célok", "Ölés", "Jutalmak", "Üres — nyomj +",
            "Választ", "Válassz tárgyat", "Mégse", "Tárgyak keresése…", "Nincs találat",
            "Minden típus", "Pontos", "Tárgyak elvétele (ki = csak észlelés)", "Elvétel átvételkor", "Csak észlelés",
            "Készítés", "Birtoklás", "ikonok: craft · mind  |  elvétel — fent",
            "Consume", "Craft", "Mind", "Ölés",
            "Kattints egy lény kiválasztásához", "Válassz megölendő lényt", "Lények keresése…", "Nincs találat",
            "Db", "MENTÉS");
        Categories(d,
            "Legenda", "EMBERSTAR ÚT",
            "Kőkorszak", "KŐKORSZAK", "Fazekasság", "FAZEKASSÁG",
            "Rézkorszak", "RÉZKORSZAK", "Bronzkorszak", "BRONZKORSZAK",
            "Vaskorszak", "VASKORSZAK", "Acélkorszak", "ACÉLKORSZAK",
            "Vadászat", "VADÁSZAT", "Földművelés", "FÖLDMŰVELÉS",
            "Kézművesség", "KÉZMŰVESSÉG", "Vándorlások", "VÁNDORLÁSOK",
            "Ág {0}", "ÁG {0}");
        return d;
    }

    static Dictionary<string, string> Ro()
    {
        var d = Pack(
            "MISIUNI", "ÎNCHIDE", "Această ramură nu are încă misiuni.", "Jurnal de misiuni",
            "MISIUNE: ", "FINALIZATĂ", "Obiective:", "Recompense:", "Text informativ",
            "ÎNCEPE CALEA", "CALE ÎNCEPUTĂ", "SE PROCESSEAZĂ...", "REUȘIT", "CONTINUĂ", "PRIMIT", "RIDICĂ RECOMPENSA",
            "Craft", "Craft și predare", "Predare", "Obține", "Omoară",
            "Craftează obiectele", "Craftează și predă obiectele", "Predă obiectele pentru recompensă",
            "Ai obiectele (nu sunt luate)", "Omoară creaturile", "Vezi eticheta de sub fiecare obiect",
            "Craftat: {0}/{1}",
            "ADAUGĂ ELEMENT", "SELECTEAZĂ PRESET", "Obiective_ID {0}", "Recompense_ID {0}", "(nr.)", "Text informativ",
            "Direcție | ", "Preset | ", "Nod | ",
            "ADAUGĂ", "ȘTERGE", "GOLEȘTE", "SALVEAZĂ",
            "START", "MISIUNE", "CHECKPOINT", "OMOARĂ", "EDITOR",
            "RAMURI", "MISIUNI",
            "Ramură: {0}", "Selectează o ramură sau creează una", "SELECTEAZĂ RAMURA", "Ramură \"{0}\"",
            "+ RAMURĂ", "Ramură nouă de misiuni", "Numele ramurii", "Limbă", "Introdu numele", "Antet: {0}",
            "CREEAZĂ", "ANULEAZĂ", "EDITARE",
            "Click pentru a alege", "Click pentru a alege", "Alege iconița", "ÎNCHIDE",
            "ȘTERGE", "Editează ramura", "SALVEAZĂ", "Ștergi ramura?", "Ștergi ramura \"{0}\"? Nu se poate anula.", "ȘTERGE",
            "ÎNCHIDE",
            "SELECT.", "NOU", "LINIE", "ȘTERGE", "GRILĂ",
            "SELECT.", "MISIUNE NOUĂ", "LEGĂTURĂ", "ȘTERGE", "SALVEAZĂ", "RESET", "EDITARE",
            "GRILĂ", "IEȘIRE", "RAMURI", "MISIUNI", "CREEAZĂ", "EDITARE", "ȘTERGE RAMURA",
            "Iconiță", "Editor", "START", "MISIUNE", "CHECKP.", "OMOARĂ",
            "Trage misiunile pentru a muta", "Selectat #{0}", "Click pe o misiune pentru editare", "Click pe graf pentru plasare",
            "Legătură: nod start", "Legătură: nod final", "Click pe un nod pentru ștergere", "Selectează sau creează un nod",
            "Editare #{0}", "Misiune #{0}", "Selectează o unealtă",
            "Selectare misiune", "Misiune #{0}", "Obiective", "Omoară", "Recompense", "Gol — apasă +",
            "Alege", "Alege un obiect", "Anulează", "Caută obiecte…", "Niciun rezultat",
            "Toate tipurile", "Exact", "Consumă obiecte (off = doar detectare)", "Consumă la revendicare", "Doar detectare",
            "Craft", "Deține", "iconițe: craft · toate  |  consumă — sus",
            "Consume", "Craft", "Toate", "Omoară",
            "Click pentru a alege o creatură", "Alege o creatură de omorât", "Caută creaturi…", "Niciun rezultat",
            "Cant.", "SALVEAZĂ");
        Categories(d,
            "Legendă", "CALEA EMBERSTAR",
            "Epoca de piatră", "EPOCA DE PIATRĂ", "Olărit", "OLĂRIT",
            "Epoca cuprului", "EPOCA CUPRULUI", "Epoca bronzului", "EPOCA BRONZULUI",
            "Epoca fierului", "EPOCA FIERULUI", "Epoca oțelului", "EPOCA OȚELULUI",
            "Vânătoare", "VÂNĂTOARE", "Agricultură", "AGRICULTURĂ",
            "Meșteșuguri", "MEȘTEȘUGURI", "Pribegii", "PRIBEGII",
            "Ramură {0}", "RAMURĂ {0}");
        return d;
    }

    static Dictionary<string, string> Tr()
    {
        var d = Pack(
            "GÖREVLER", "KAPAT", "Bu dalda henüz görev yok.", "Görev günlüğü",
            "GÖREV: ", "TAMAMLANDI", "Hedefler:", "Ödüller:", "Bilgi metni",
            "YOLA BAŞLA", "YOL BAŞLADI", "İŞLENİYOR...", "GEÇİLDİ", "DEVAM", "ALINDI", "ÖDÜLÜ AL",
            "Üret", "Üret ve teslim et", "Teslim et", "Elde et", "Öldür",
            "Eşyaları üret", "Eşyaları üret ve teslim et", "Ödül için eşyaları teslim et",
            "Eşyalara sahip ol (alınmaz)", "Yaratıkları öldür", "Her eşyanın altındaki etikete bak",
            "Üretildi: {0}/{1}",
            "ÖĞE EKLE", "ÖN AYAR SEÇ", "Hedefler_ID {0}", "Ödüller_ID {0}", "(adet)", "Bilgi metni",
            "Yön | ", "Ön ayar | ", "Düğüm | ",
            "EKLE", "SİL", "TEMİZLE", "KAYDET",
            "BAŞLANGIÇ", "GÖREV", "KONTROL NOKTASI", "ÖLDÜR", "EDITÖR",
            "DALLAR", "GÖREVLER",
            "Dal: {0}", "Bir dal seç veya yeni oluştur", "DAL SEÇ", "Dal \"{0}\"",
            "+ DAL", "Yeni görev dalı", "Dal adı", "Dil", "Dal adını gir", "Başlık: {0}",
            "OLUŞTUR", "İPTAL", "DÜZENLE",
            "Seçmek için tıkla", "Seçmek için tıkla", "Simge seç", "KAPAT",
            "SİL", "Dalı düzenle", "KAYDET", "Dal silinsin mi?", "\"{0}\" dalı silinsin mi? Geri alınamaz.", "SİL",
            "KAPAT",
            "SEÇ", "YENİ", "ÇİZGİ", "SİL", "IZGARA",
            "SEÇ", "YENİ GÖREV", "BAĞLA", "SİL", "KAYDET", "SIFIRLA", "DÜZENLE",
            "IZGARA", "ÇIKIŞ", "DALLAR", "GÖREVLER", "OLUŞTUR", "DÜZENLE", "DALI SİL",
            "Simge", "Editör", "BAŞLANGIÇ", "GÖREV", "KONTROL", "ÖLDÜR",
            "Taşımak için görevleri sürükle", "Seçili #{0}", "Düzenlemek için göreve tıkla", "Yerleştirmek için grafa tıkla",
            "Bağ: başlangıç düğümü", "Bağ: bitiş düğümü", "Silmek için düğüme tıkla", "Düğüm seç veya oluştur",
            "Düzenleniyor #{0}", "Görev #{0}", "Bir araç seç",
            "Görev seçimi", "Görev #{0}", "Hedefler", "Öldür", "Ödüller", "Boş — + bas",
            "Seç", "Bir eşya seç", "İptal", "Eşya ara…", "Sonuç yok",
            "Tüm türler", "Tam", "Eşyaları al (kapalı = sadece algıla)", "Teslimde al", "Sadece algıla",
            "Üret", "Sahip ol", "simgeler: craft · tümü  |  al — üstte",
            "Consume", "Craft", "Tüm türler", "Öldür",
            "Yaratık seçmek için tıkla", "Öldürülecek yaratığı seç", "Yaratık ara…", "Sonuç yok",
            "Adet", "KAYDET");
        Categories(d,
            "Efsane", "EMBERSTAR YOLU",
            "Taş Çağı", "TAŞ ÇAĞI", "Çömlekçilik", "ÇÖMLEKÇİLİK",
            "Bakır Çağı", "BAKIR ÇAĞI", "Tunç Çağı", "TUNÇ ÇAĞI",
            "Demir Çağı", "DEMİR ÇAĞI", "Çelik Çağı", "ÇELİK ÇAĞI",
            "Avcılık", "AVCILIK", "Tarım", "TARIM",
            "Zanaat", "ZANAAT", "Gezintiler", "GEZİNTİLER",
            "Dal {0}", "DAL {0}");
        return d;
    }

    static Dictionary<string, string> Ja()
    {
        var d = Pack(
            "クエスト", "閉じる", "この分岐にはまだクエストがありません。", "クエスト日誌",
            "クエスト: ", "完了", "目標:", "報酬:", "情報テキスト",
            "道を始める", "道を開始した", "処理中...", "達成", "続ける", "受取済み", "報酬を受け取る",
            "クラフト", "クラフトして納品", "納品", "入手", "討伐",
            "アイテムをクラフトする", "クラフトして納品する", "報酬のためにアイテムを納品する",
            "アイテムを所持する（没収されない）", "クリーチャーを倒す", "各アイテム下のラベルを確認",
            "クラフト済: {0}/{1}",
            "要素を追加", "プリセットを選択", "目標_ID {0}", "報酬_ID {0}", "(数)", "情報テキスト",
            "方向 | ", "プリセット | ", "ノード | ",
            "追加", "削除", "クリア", "保存",
            "スタート", "クエスト", "チェックポイント", "討伐", "編集",
            "分岐", "クエスト",
            "分岐: {0}", "分岐を選択するか新規作成", "分岐を選択", "分岐「{0}」",
            "+ 分岐", "新しいクエスト分岐", "分岐名", "言語", "分岐名を入力", "ヘッダー: {0}",
            "作成", "キャンセル", "編集",
            "クリックして選択", "クリックして選択", "アイコンを選択", "閉じる",
            "削除", "分岐を編集", "保存", "分岐を削除？", "分岐「{0}」を削除しますか？元に戻せません。", "削除",
            "閉じる",
            "選択", "新規", "線", "削除", "グリッド",
            "選択", "新規クエスト", "接続", "削除", "保存", "リセット", "編集",
            "グリッド", "終了", "分岐", "クエスト", "作成", "編集", "分岐を削除",
            "アイコン", "編集", "スタート", "クエスト", "チェック", "討伐",
            "ドラッグして移動", "選択中 #{0}", "クエストをクリックして編集", "グラフをクリックして配置",
            "接続: 開始ノード", "接続: 終了ノード", "ノードをクリックして削除", "選択または作成",
            "編集中 #{0}", "クエスト #{0}", "ツールを選択",
            "クエスト選択", "クエスト #{0}", "目標", "討伐", "報酬", "空 — + を押す",
            "選択", "アイテムを選ぶ", "キャンセル", "アイテムを検索…", "一致なし",
            "全種類", "完全一致", "アイテムを消費（オフ＝検出のみ）", "受取時に消費", "検出のみ",
            "クラフト", "所持", "アイコン: craft · 全種類  |  消費 — 上",
            "消費", "クラフト", "全種類", "討伐",
            "クリックしてクリーチャーを選択", "討伐するクリーチャーを選択", "クリーチャーを検索…", "一致なし",
            "数量", "保存");
        Categories(d,
            "伝説", "EMBERSTARの道",
            "石器時代", "石器時代", "陶芸", "陶芸",
            "銅器時代", "銅器時代", "青銅器時代", "青銅器時代",
            "鉄器時代", "鉄器時代", "鋼鉄時代", "鋼鉄時代",
            "狩猟", "狩猟", "農業", "農業",
            "工芸", "工芸", "放浪", "放浪",
            "分岐 {0}", "分岐 {0}");
        return d;
    }

    static Dictionary<string, string> Ko()
    {
        var d = Pack(
            "퀘스트", "닫기", "이 분기에는 아직 퀘스트가 없습니다.", "퀘스트 일지",
            "퀘스트: ", "완료", "목표:", "보상:", "정보 텍스트",
            "길 시작", "길 시작됨", "처리 중...", "통과", "계속", "받음", "보상 받기",
            "제작", "제작 후 제출", "제출", "획득", "처치",
            "아이템을 제작하세요", "제작하고 제출하세요", "보상을 위해 아이템을 제출하세요",
            "아이템을 보유하세요 (가져가지 않음)", "생물을 처치하세요", "각 아이템 아래 라벨을 확인하세요",
            "제작됨: {0}/{1}",
            "항목 추가", "프리셋 선택", "목표_ID {0}", "보상_ID {0}", "(수량)", "정보 텍스트",
            "방향 | ", "프리셋 | ", "노드 | ",
            "추가", "삭제", "지우기", "저장",
            "시작", "퀘스트", "체크포인트", "처치", "편집기",
            "분기", "퀘스트",
            "분기: {0}", "분기를 선택하거나 새로 만드세요", "분기 선택", "분기 \"{0}\"",
            "+ 분기", "새 퀘스트 분기", "분기 이름", "언어", "분기 이름 입력", "헤더: {0}",
            "생성", "취소", "편집",
            "클릭하여 선택", "클릭하여 선택", "아이콘 선택", "닫기",
            "삭제", "분기 편집", "저장", "분기를 삭제할까요?", "분기 \"{0}\"을(를) 삭제할까요? 되돌릴 수 없습니다.", "삭제",
            "닫기",
            "선택", "새로", "선", "삭제", "격자",
            "선택", "새 퀘스트", "연결", "삭제", "저장", "초기화", "편집",
            "격자", "종료", "분기", "퀘스트", "생성", "편집", "분기 삭제",
            "아이콘", "편집기", "시작", "퀘스트", "체크", "처치",
            "드래그하여 이동", "선택됨 #{0}", "퀘스트를 클릭하여 편집", "그래프를 클릭하여 배치",
            "연결: 시작 노드", "연결: 끝 노드", "노드를 클릭하여 삭제", "선택하거나 생성",
            "편집 중 #{0}", "퀘스트 #{0}", "도구 선택",
            "퀘스트 선택", "퀘스트 #{0}", "목표", "처치", "보상", "비어 있음 — + 누르기",
            "선택", "아이템 선택", "취소", "아이템 검색…", "결과 없음",
            "모든 유형", "정확", "아이템 소모 (끔 = 감지만)", "수령 시 소모", "감지만",
            "제작", "보유", "아이콘: craft · 모든 유형  |  소모 — 위",
            "소모", "제작", "모든 유형", "처치",
            "클릭하여 생물 선택", "처치할 생물 선택", "생물 검색…", "결과 없음",
            "수량", "저장");
        Categories(d,
            "전설", "EMBERSTAR 길",
            "석기 시대", "석기 시대", "도자기", "도자기",
            "구리 시대", "구리 시대", "청동기 시대", "청동기 시대",
            "철기 시대", "철기 시대", "강철 시대", "강철 시대",
            "사냥", "사냥", "농업", "농업",
            "공예", "공예", "방랑", "방랑",
            "분기 {0}", "분기 {0}");
        return d;
    }

    static Dictionary<string, string> ZhCn()
    {
        var d = Pack(
            "任务", "关闭", "该分支尚无任务。", "任务日志",
            "任务: ", "已完成", "目标:", "奖励:", "信息文本",
            "开始道路", "道路已开始", "处理中...", "已通过", "继续", "已领取", "领取奖励",
            "制作", "制作并上交", "上交", "获得", "击杀",
            "制作物品", "制作并上交物品", "上交物品以领取奖励",
            "持有物品（不会被拿走）", "击杀生物", "查看每个物品下的标签",
            "已制作: {0}/{1}",
            "添加元素", "选择预设", "目标_ID {0}", "奖励_ID {0}", "(数量)", "信息文本",
            "方向 | ", "预设 | ", "节点 | ",
            "添加", "删除", "清空", "保存",
            "开始", "任务", "检查点", "击杀", "编辑器",
            "分支", "任务",
            "分支: {0}", "选择分支或新建", "选择分支", "分支 \"{0}\"",
            "+ 分支", "新任务分支", "分支名称", "语言", "输入分支名称", "标题: {0}",
            "创建", "取消", "编辑",
            "点击选择", "点击选择", "选择图标", "关闭",
            "删除", "编辑分支", "保存", "删除分支？", "删除分支 \"{0}\"？此操作无法撤销。", "删除",
            "关闭",
            "选择", "新建", "连线", "删除", "网格",
            "选择", "新任务", "连接", "删除", "保存", "重置", "编辑",
            "网格", "退出", "分支", "任务", "创建", "编辑", "删除分支",
            "图标", "编辑器", "开始", "任务", "检查点", "击杀",
            "拖动任务以移动", "已选 #{0}", "点击任务以编辑", "点击图以放置",
            "连接: 起始节点", "连接: 结束节点", "点击节点以删除", "选择或创建节点",
            "编辑中 #{0}", "任务 #{0}", "选择工具",
            "选择任务", "任务 #{0}", "目标", "击杀", "奖励", "空 — 按 +",
            "选择", "选择物品", "取消", "搜索物品…", "无匹配",
            "所有类型", "精确", "消耗物品（关 = 仅检测）", "领取时消耗", "仅检测",
            "制作", "持有", "图标: craft · 全部  |  消耗 — 上方",
            "消耗", "制作", "全部类型", "击杀",
            "点击选择生物", "选择要击杀的生物", "搜索生物…", "无匹配",
            "数量", "保存");
        Categories(d,
            "传说", "EMBERSTAR 之路",
            "石器时代", "石器时代", "制陶", "制陶",
            "铜器时代", "铜器时代", "青铜时代", "青铜时代",
            "铁器时代", "铁器时代", "钢铁时代", "钢铁时代",
            "狩猎", "狩猎", "农业", "农业",
            "工艺", "工艺", "漫游", "漫游",
            "分支 {0}", "分支 {0}");
        return d;
    }

    static Dictionary<string, string> ZhTw()
    {
        var d = Pack(
            "任務", "關閉", "此分支尚無任務。", "任務日誌",
            "任務: ", "已完成", "目標:", "獎勵:", "資訊文字",
            "開始道路", "道路已開始", "處理中...", "已通過", "繼續", "已領取", "領取獎勵",
            "製作", "製作並繳交", "繳交", "取得", "擊殺",
            "製作物品", "製作並繳交物品", "繳交物品以領取獎勵",
            "持有物品（不會被拿走）", "擊殺生物", "查看每個物品下的標籤",
            "已製作: {0}/{1}",
            "新增元素", "選擇預設", "目標_ID {0}", "獎勵_ID {0}", "(數量)", "資訊文字",
            "方向 | ", "預設 | ", "節點 | ",
            "新增", "刪除", "清空", "儲存",
            "開始", "任務", "檢查點", "擊殺", "編輯器",
            "分支", "任務",
            "分支: {0}", "選擇分支或新建", "選擇分支", "分支 \"{0}\"",
            "+ 分支", "新任務分支", "分支名稱", "語言", "輸入分支名稱", "標題: {0}",
            "建立", "取消", "編輯",
            "點擊選擇", "點擊選擇", "選擇圖示", "關閉",
            "刪除", "編輯分支", "儲存", "刪除分支？", "刪除分支 \"{0}\"？此操作無法復原。", "刪除",
            "關閉",
            "選擇", "新建", "連線", "刪除", "網格",
            "選擇", "新任務", "連接", "刪除", "儲存", "重設", "編輯",
            "網格", "離開", "分支", "任務", "建立", "編輯", "刪除分支",
            "圖示", "編輯器", "開始", "任務", "檢查點", "擊殺",
            "拖曳任務以移動", "已選 #{0}", "點擊任務以編輯", "點擊圖以放置",
            "連接: 起始節點", "連接: 結束節點", "點擊節點以刪除", "選擇或建立節點",
            "編輯中 #{0}", "任務 #{0}", "選擇工具",
            "選擇任務", "任務 #{0}", "目標", "擊殺", "獎勵", "空 — 按 +",
            "選擇", "選擇物品", "取消", "搜尋物品…", "無符合",
            "所有類型", "精確", "消耗物品（關 = 僅偵測）", "領取時消耗", "僅偵測",
            "製作", "持有", "圖示: craft · 全部  |  消耗 — 上方",
            "消耗", "製作", "全部類型", "擊殺",
            "點擊選擇生物", "選擇要擊殺的生物", "搜尋生物…", "無符合",
            "數量", "儲存");
        Categories(d,
            "傳說", "EMBERSTAR 之路",
            "石器時代", "石器時代", "製陶", "製陶",
            "銅器時代", "銅器時代", "青銅時代", "青銅時代",
            "鐵器時代", "鐵器時代", "鋼鐵時代", "鋼鐵時代",
            "狩獵", "狩獵", "農業", "農業",
            "工藝", "工藝", "漫遊", "漫遊",
            "分支 {0}", "分支 {0}");
        return d;
    }

    static Dictionary<string, string> Ar()
    {
        var d = Pack(
            "المهام", "إغلاق", "لا توجد مهام في هذا الفرع بعد.", "سجل المهام",
            "مهمة: ", "مكتملة", "الأهداف:", "المكافآت:", "نص معلوماتي",
            "ابدأ المسار", "بدأ المسار", "جاري المعالجة...", "تم", "متابعة", "تم الاستلام", "استلم المكافأة",
            "اصنع", "اصنع وسلّم", "سلّم", "احصل", "اقتل",
            "اصنع العناصر", "اصنع وسلّم العناصر", "سلّم العناصر مقابل المكافأة",
            "امتلك العناصر (لا تُؤخذ)", "اقتل المخلوقات", "انظر التسمية تحت كل عنصر",
            "مصنوع: {0}/{1}",
            "أضف عنصرًا", "اختر إعدادًا مسبقًا", "أهداف_ID {0}", "مكافآت_ID {0}", "(عدد)", "نص معلوماتي",
            "الاتجاه | ", "إعداد مسبق | ", "عقدة | ",
            "إضافة", "حذف", "مسح", "حفظ",
            "بداية", "مهمة", "نقطة تفتيش", "قتل", "محرر",
            "فروع", "مهام",
            "الفرع: {0}", "اختر فرعًا أو أنشئ واحدًا", "اختر فرعًا", "فرع \"{0}\"",
            "+ فرع", "فرع مهام جديد", "اسم الفرع", "اللغة", "أدخل اسم الفرع", "العنوان: {0}",
            "إنشاء", "إلغاء", "تعديل",
            "انقر للاختيار", "انقر للاختيار", "اختر أيقونة", "إغلاق",
            "حذف", "تعديل الفرع", "حفظ", "حذف الفرع؟", "حذف الفرع \"{0}\"؟ لا يمكن التراجع.", "حذف",
            "إغلاق",
            "تحديد", "جديد", "خط", "حذف", "شبكة",
            "تحديد", "مهمة جديدة", "ربط", "حذف", "حفظ", "إعادة", "تعديل",
            "شبكة", "خروج", "فروع", "مهام", "إنشاء", "تعديل", "حذف الفرع",
            "أيقونة", "محرر", "بداية", "مهمة", "نقطة", "قتل",
            "اسحب المهام للتحريك", "محدد #{0}", "انقر مهمة للتعديل", "انقر الرسم للوضع",
            "ربط: عقدة البداية", "ربط: عقدة النهاية", "انقر عقدة للحذف", "حدد أو أنشئ عقدة",
            "تعديل #{0}", "مهمة #{0}", "اختر أداة",
            "اختيار مهمة", "مهمة #{0}", "الأهداف", "قتل", "المكافآت", "فارغ — اضغط +",
            "اختر", "اختر عنصرًا", "إلغاء", "ابحث عن عناصر…", "لا نتائج",
            "كل الأنواع", "دقيق", "استهلك العناصر (إيقاف = كشف فقط)", "استهلك عند الاستلام", "كشف فقط",
            "صنع", "امتلك", "أيقونات: craft · الكل  |  استهلاك — أعلى",
            "استهلاك", "صنع", "كل الأنواع", "قتل",
            "انقر لاختيار مخلوق", "اختر مخلوقًا للقتل", "ابحث عن مخلوقات…", "لا نتائج",
            "الكمية", "حفظ");
        Categories(d,
            "أسطورة", "طريق إمبرستار",
            "العصر الحجري", "العصر الحجري", "فخار", "فخار",
            "عصر النحاس", "عصر النحاس", "العصر البرونزي", "العصر البرونزي",
            "العصر الحديدي", "العصر الحديدي", "عصر الفولاذ", "عصر الفولاذ",
            "صيد", "صيد", "زراعة", "زراعة",
            "حِرف", "حِرف", "تجوال", "تجوال",
            "فرع {0}", "فرع {0}");
        return d;
    }

    static Dictionary<string, string> Eo()
    {
        var d = Pack(
            "QUESTOJ", "FERMI", "Ĉi tiu branĉo ankoraŭ ne havas questojn.", "Questa taglibro",
            "QUESTO: ", "FINITA", "Celoj:", "Premioj:", "Informa teksto",
            "KOMENCI VOJON", "VOJO KOMENCITA", "PRILABORADO...", "TRAPASITA", "DAŬRIGI", "RICEVITA", "PRENI PREMION",
            "Krei", "Krei kaj liveri", "Liveri", "Akiri", "Mortigi",
            "Kreu la aĵojn", "Kreu kaj liveru aĵojn", "Liveru aĵojn por la premio",
            "Havu la aĵojn (ne forprenataj)", "Mortigu la estaĵojn", "Vidu etikedon sub ĉiu aĵo",
            "Kreita: {0}/{1}",
            "ALDONI ELEMENTON", "ELEKTI PRESETON", "Celoj_ID {0}", "Premioj_ID {0}", "(nombro)", "Informa teksto",
            "Direkto | ", "Preseto | ", "Nodo | ",
            "ALDONI", "FORIGI", "VIŜI", "KONSERVI",
            "STARTO", "QUESTO", "KONTROLPUNto", "MORTIGI", "REDAKTILO",
            "BRANĈOJ", "QUESTOJ",
            "Branĉo: {0}", "Elektu branĉon aŭ kreu novan", "ELEKTU BRANĈON", "Branĉo \"{0}\"",
            "+ BRANĈO", "Nova questa branĉo", "Nomo de branĉo", "Lingvo", "Enigu nomon", "Kapo: {0}",
            "KREI", "NULIGI", "RED.",
            "Klaku por elekti", "Klaku por elekti", "Elekti piktogramon", "FERMI",
            "FORIGI", "Redakti branĉon", "KONSERVI", "Forigi branĉon?", "Forigi branĉon \"{0}\"? Ne malfarebla.", "FORIGI",
            "FERMI",
            "ELEKTI", "NOVA", "LINIO", "FORIGI", "KRADO",
            "ELEKTI", "NOVA QUESTO", "LIGI", "FORIGI", "KONSERVI", "RESET", "RED.",
            "KRADO", "ELIRI", "BRANĈOJ", "QUESTOJ", "KREI", "RED.", "FORIGI BRANĈON",
            "Piktogramo", "Redaktilo", "STARTO", "QUESTO", "KONTROL.", "MORTIGI",
            "Tiri questojn por movi", "Elektita #{0}", "Klaku queston por redakti", "Klaku grafon por meti",
            "Ligo: komenca nodo", "Ligo: fina nodo", "Klaku nodon por forigi", "Elektu aŭ kreu nodon",
            "Redaktas #{0}", "Questo #{0}", "Elektu ilon",
            "Elekto de questo", "Questo #{0}", "Celoj", "Mortigi", "Premioj", "Malplena — premu +",
            "Elekti", "Elektu aĵon", "Nuligi", "Serĉi aĵojn…", "Neniuj rezultoj",
            "Ĉiuj tipoj", "Preciza", "Konsumi aĵojn (off = nur detekti)", "Konsumi je ricevo", "Nur detekti",
            "Krei", "Havi", "piktogramoj: craft · ĉiuj  |  konsumi — supre",
            "Consume", "Craft", "Ĉiuj tipoj", "Mortigi",
            "Klaku por elekti estaĵon", "Elektu estaĵon por mortigi", "Serĉi estaĵojn…", "Neniuj rezultoj",
            "Nombro", "KONSERVI");
        Categories(d,
            "Legendo", "EMBERSTAR VOJO",
            "Ŝtonepoko", "ŜTONEPOKO", "Ceramiko", "CERAMIKO",
            "Kuprepoko", "KUPREPOKO", "Bronzepoko", "BRONZEPOKO",
            "Ferepoko", "FEREPOKO", "Ŝtalepoko", "ŜTALEPOKO",
            "Ĉasado", "ĈASADO", "Agrikulturo", "AGRIKULTURO",
            "Metioj", "METIOJ", "Vagadoj", "VAGADOJ",
            "Branĉo {0}", "BRANĈO {0}");
        return d;
    }

    static Dictionary<string, string> Is()
    {
        var d = Pack(
            "VERKEFNI", "LOKA", "Þessi grein hefur engar verkefni enn.", "Verkefnadagbók",
            "VERKEFNI: ", "LOKIÐ", "Markmið:", "Verðlaun:", "Upplýsingatexti",
            "BYRJA LEIÐ", "LEIÐ HAFIÐ", "VINNSLA...", "STAÐIST", "HALDA ÁFRAM", "MÓTTEKIÐ", "SÆKJA VERÐLAUN",
            "Búa til", "Búa til og skila", "Skila", "Fá", "Drepa",
            "Búðu til hlutina", "Búðu til og skilaðu", "Skilaðu hlutum fyrir verðlaun",
            "Hafðu hlutina (ekki teknir)", "Drepdu verurnar", "Sjá merki undir hverjum hlut",
            "Búið til: {0}/{1}",
            "BÆTA VIÐ ATRIÐI", "VELJA FORSTILLINGU", "Markmið_ID {0}", "Verðlaun_ID {0}", "(fjöl.)", "Upplýsingatexti",
            "Stefna | ", "Forstilling | ", "Hnútur | ",
            "BÆTA VIÐ", "EYÐA", "HREINSA", "VISTA",
            "BYRJUN", "VERKEFNI", "ATHUGUNARSTAÐUR", "DREPA", "RITSTJÓRI",
            "GREINAR", "VERKEFNI",
            "Grein: {0}", "Veldu grein eða búðu til nýja", "VELDU GREIN", "Grein \"{0}\"",
            "+ GREIN", "Ný verkefnagreinn", "Nafn greinar", "Tungumál", "Sláðu inn nafn", "Fyrirsögn: {0}",
            "BÚA TIL", "HÆTTA VIÐ", "BREYTA",
            "Smelltu til að velja", "Smelltu til að velja", "Velja tákn", "LOKA",
            "EYÐA", "Breyta grein", "VISTA", "Eyða grein?", "Eyða grein \"{0}\"? Ekki hægt að afturkalla.", "EYÐA",
            "LOKA",
            "VELJA", "NÝTT", "LÍNA", "EYÐA", "NET",
            "VELJA", "NÝTT VERKEFNI", "TENGSLA", "EYÐA", "VISTA", "ENDURST.", "BREYTA",
            "NET", "HÆTTA", "GREINAR", "VERKEFNI", "BÚA TIL", "BREYTA", "EYÐA GREIN",
            "Tákn", "Ritstjóri", "BYRJUN", "VERKEFNI", "ATHUG.", "DREPA",
            "Dragðu verkefni til að færa", "Valið #{0}", "Smelltu á verkefni til að breyta", "Smelltu á graf til að setja",
            "Tengsl: upphafshnútur", "Tengsl: endahnútur", "Smelltu á hnútt til að eyða", "Veldu eða búðu til hnútt",
            "Breyti #{0}", "Verkefni #{0}", "Veldu tól",
            "Val á verkefni", "Verkefni #{0}", "Markmið", "Drepa", "Verðlaun", "Tómt — ýttu á +",
            "Velja", "Veldu hlut", "Hætta við", "Leita að hlutum…", "Engar niðurstöður",
            "Allar gerðir", "Nákvæmt", "Taka hluti (af = aðeins greina)", "Taka við innlausn", "Aðeins greina",
            "Búa til", "Hafa", "tákn: craft · allar  |  taka — efst",
            "Consume", "Craft", "Allar gerðir", "Drepa",
            "Smelltu til að velja veru", "Veldu veru til að drepa", "Leita að verum…", "Engar niðurstöður",
            "Fjöldi", "VISTA");
        Categories(d,
            "Goðsögn", "EMBERSTAR-LEIÐ",
            "Steinaldur", "STEINALDUR", "Leirkeragerð", "LEIRKERAGERÐ",
            "Koparöld", "KOPARÖLD", "Bronsöld", "BRONSÖLD",
            "Járnöld", "JÁRNÖLD", "Stálöld", "STÁLÖLD",
            "Veiði", "VEIÐI", "Landbúnaður", "LANDBÚNAÐUR",
            "Handverk", "HANDVERK", "Flakk", "FLAKK",
            "Grein {0}", "GREIN {0}");
        return d;
    }

    static Dictionary<string, string> Lt()
    {
        var d = Pack(
            "UŽDUOTYS", "UŽDARYTI", "Šioje šakoje dar nėra užduočių.", "Užduočių žurnalas",
            "UŽDUOTIS: ", "ATLIKTA", "Tikslai:", "Apdovanojimai:", "Informacinis tekstas",
            "PRADĖTI KELIĄ", "KELIAS PRADEDTAS", "APDOROJAMA...", "ĮVEIKTA", "TĘSTI", "GAVOTE", "ATSIIMTI ATDOVANOJIMĄ",
            "Gaminti", "Gaminti ir atiduoti", "Atiduoti", "Gauti", "Nužudyti",
            "Pagaminkite daiktus", "Pagaminkite ir atiduokite", "Atiduokite daiktus už atlygį",
            "Turėkite daiktus (neatimami)", "Nužudykite būtybes", "Žiūrėkite etiketę po kiekvienu daiktu",
            "Pagaminta: {0}/{1}",
            "PRIDĖTI ELEMENTĄ", "PASIRINKTI ŠABLONĄ", "Tikslai_ID {0}", "Apdovanojimai_ID {0}", "(sk.)", "Informacinis tekstas",
            "Kryptis | ", "Šablonas | ", "Mazgas | ",
            "PRIDĖTI", "IŠTRINTI", "IŠVALYTI", "IŠSAUGOTI",
            "PRADŽIA", "UŽDUOTIS", "KONTROLINIS", "NUŽUDYTI", "REDAKTORIUS",
            "ŠAKOS", "UŽDUOTYS",
            "Šaka: {0}", "Pasirinkite šaką arba sukurkite naują", "PASIRINKITE ŠAKĄ", "Šaka \"{0}\"",
            "+ ŠAKA", "Nauja užduočių šaka", "Šakos pavadinimas", "Kalba", "Įveskite pavadinimą", "Antraštė: {0}",
            "SUKURTI", "ATŠAUKTI", "REDAGUOTI",
            "Spustelėkite norėdami pasirinkti", "Spustelėkite norėdami pasirinkti", "Pasirinkti piktogramą", "UŽDARYTI",
            "IŠTRINTI", "Redaguoti šaką", "IŠSAUGOTI", "Ištrinti šaką?", "Ištrinti šaką \"{0}\"? Negalima atšaukti.", "IŠTRINTI",
            "UŽDARYTI",
            "PASIRINKTI", "NAUJAS", "LINIJA", "IŠTRINTI", "TINKLAS",
            "PASIRINKTI", "NAUJA UŽDUOTIS", "JUNGTI", "IŠTRINTI", "IŠSAUGOTI", "ATSTATYTI", "REDAGUOTI",
            "TINKLAS", "IŠEITI", "ŠAKOS", "UŽDUOTYS", "SUKURTI", "REDAGUOTI", "IŠTRINTI ŠAKĄ",
            "Piktograma", "Redaktorius", "PRADŽIA", "UŽDUOTIS", "KONTROL.", "NUŽUDYTI",
            "Vilkite užduotis norėdami perkelti", "Pasirinkta #{0}", "Spustelėkite užduotį redagavimui", "Spustelėkite grafiką padėjimui",
            "Jungtis: pradžios mazgas", "Jungtis: pabaigos mazgas", "Spustelėkite mazgą trynimui", "Pasirinkite arba sukurkite mazgą",
            "Redaguojama #{0}", "Užduotis #{0}", "Pasirinkite įrankį",
            "Užduoties pasirinkimas", "Užduotis #{0}", "Tikslai", "Nužudyti", "Apdovanojimai", "Tuščia — spauskite +",
            "Pasirinkti", "Pasirinkite daiktą", "Atšaukti", "Ieškoti daiktų…", "Nieko nerasta",
            "Visi tipai", "Tiksliai", "Paimti daiktus (išj. = tik aptikti)", "Paimti atsiimant", "Tik aptikti",
            "Gaminti", "Turėti", "piktogramos: craft · visi  |  paimti — viršuje",
            "Consume", "Craft", "Visi tipai", "Nužudyti",
            "Spustelėkite norėdami pasirinkti būtybę", "Pasirinkite būtybę nužudymui", "Ieškoti būtybių…", "Nieko nerasta",
            "Kiekis", "IŠSAUGOTI");
        Categories(d,
            "Legenda", "EMBERSTAR KELIAS",
            "Akmens amžius", "AKMENS AMŽIUS", "Puodininkystė", "PUODININKYSTĖ",
            "Vario amžius", "VARIO AMŽIUS", "Bronzos amžius", "BRONZOS AMŽIUS",
            "Geležies amžius", "GELEŽIES AMŽIUS", "Plieno amžius", "PLIENO AMŽIUS",
            "Medžioklė", "MEDŽIOKLĖ", "Žemdirbystė", "ŽEMDIRBYSTĖ",
            "Amatas", "AMATAS", "Klajonės", "KLAJONĖS",
            "Šaka {0}", "ŠAKA {0}");
        return d;
    }

    static Dictionary<string, string> Sr()
    {
        var d = Pack(
            "ZADACI", "ZATVORI", "Ova grana još nema zadataka.", "Dnevnik zadataka",
            "ZADATAK: ", "ZAVRŠENO", "Ciljevi:", "Nagrade:", "Informativni tekst",
            "ZAPOČNI PUT", "PUT ZAPOČET", "OBRADA...", "PROŠLO", "NASTAVI", "PRIMLJENO", "PREUZMI NAGRADU",
            "Napravi", "Napravi i predaj", "Predaj", "Nabavi", "Ubij",
            "Napravi predmete", "Napravi i predaj predmete", "Predaj predmete za nagradu",
            "Imaj predmete (ne oduzimaju se)", "Ubij stvorenja", "Vidi oznaku ispod svakog predmeta",
            "Napravljeno: {0}/{1}",
            "DODAJ ELEMENT", "IZABERI PRESET", "Ciljevi_ID {0}", "Nagrade_ID {0}", "(br.)", "Informativni tekst",
            "Smer | ", "Preset | ", "Čvor | ",
            "DODAJ", "OBRIŠI", "OČISTI", "SAČUVAJ",
            "START", "ZADATAK", "CHECKPOINT", "UBIJ", "UREĐIVAČ",
            "GRANE", "ZADACI",
            "Grana: {0}", "Izaberi granu ili napravi novu", "IZABERI GRANU", "Grana \"{0}\"",
            "+ GRANA", "Nova grana zadataka", "Ime grane", "Jezik", "Unesi ime grane", "Zaglavlje: {0}",
            "NAPRAVI", "OTKAŽI", "UREDI",
            "Klikni da izabereš", "Klikni da izabereš", "Izaberi ikonu", "ZATVORI",
            "OBRIŠI", "Uredi granu", "SAČUVAJ", "Obrisati granu?", "Obrisati granu \"{0}\"? Ne može se opozvati.", "OBRIŠI",
            "ZATVORI",
            "IZBOR", "NOVO", "LINIJA", "OBRIŠI", "MREŽA",
            "IZBOR", "NOVI ZADATAK", "VEZA", "OBRIŠI", "SAČUVAJ", "RESET", "UREDI",
            "MREŽA", "IZLAZ", "GRANE", "ZADACI", "NAPRAVI", "UREDI", "OBRIŠI GRANU",
            "Ikona", "Uređivač", "START", "ZADATAK", "CHECKP.", "UBIJ",
            "Prevuci zadatke da pomeriš", "Izabrano #{0}", "Klikni zadatak za uređivanje", "Klikni graf za postavljanje",
            "Veza: početni čvor", "Veza: krajnji čvor", "Klikni čvor za brisanje", "Izaberi ili napravi čvor",
            "Uređivanje #{0}", "Zadatak #{0}", "Izaberi alat",
            "Izbor zadatka", "Zadatak #{0}", "Ciljevi", "Ubij", "Nagrade", "Prazno — pritisni +",
            "Izaberi", "Izaberi predmet", "Otkaži", "Pretraži predmete…", "Nema rezultata",
            "Svi tipovi", "Tačno", "Uzmi predmete (isklj. = samo otkrij)", "Uzmi pri preuzimanju", "Samo otkrij",
            "Napravi", "Imaj", "ikone: craft · svi  |  uzmi — gore",
            "Consume", "Craft", "Svi tipovi", "Ubij",
            "Klikni da izabereš stvorenje", "Izaberi stvorenje za ubijanje", "Pretraži stvorenja…", "Nema rezultata",
            "Kol.", "SAČUVAJ");
        Categories(d,
            "Legenda", "EMBERSTAR PUT",
            "Kameno doba", "KAMENO DOBA", "Grnčarija", "GRNČARIJA",
            "Bakarno doba", "BAKARNO DOBA", "Bronzano doba", "BRONZANO DOBA",
            "Gvozdeno doba", "GVOZDENO DOBA", "Čelično doba", "ČELIČNO DOBA",
            "Lov", "LOV", "Poljoprivreda", "POLJOPRIVREDA",
            "Zanati", "ZANATI", "Lutanja", "LUTANJA",
            "Grana {0}", "GRANA {0}");
        return d;
    }

    static Dictionary<string, string> Th()
    {
        var d = Pack(
            "เควส", "ปิด", "สาขานี้ยังไม่มีเควส", "บันทึกเควส",
            "เควส: ", "สำเร็จ", "เป้าหมาย:", "รางวัล:", "ข้อความข้อมูล",
            "เริ่มเส้นทาง", "เริ่มเส้นทางแล้ว", "กำลังประมวลผล...", "ผ่าน", "ดำเนินการต่อ", "ได้รับแล้ว", "รับรางวัล",
            "คราฟต์", "คราฟต์และส่ง", "ส่ง", "ได้รับ", "ฆ่า",
            "คราฟต์ไอเท็ม", "คราฟต์และส่งไอเท็ม", "ส่งไอเท็มเพื่อรับรางวัล",
            "มีไอเท็ม (ไม่ถูกยึด)", "ฆ่าสิ่งมีชีวิต", "ดูป้ายใต้แต่ละไอเท็ม",
            "คราฟต์แล้ว: {0}/{1}",
            "เพิ่มรายการ", "เลือกพรีเซ็ต", "เป้าหมาย_ID {0}", "รางวัล_ID {0}", "(จำนวน)", "ข้อความข้อมูล",
            "ทิศทาง | ", "พรีเซ็ต | ", "โหนด | ",
            "เพิ่ม", "ลบ", "ล้าง", "บันทึก",
            "เริ่ม", "เควส", "จุดตรวจ", "ฆ่า", "ตัวแก้ไข",
            "สาขา", "เควส",
            "สาขา: {0}", "เลือกสาขาหรือสร้างใหม่", "เลือกสาขา", "สาขา \"{0}\"",
            "+ สาขา", "สาขาเควสใหม่", "ชื่อสาขา", "ภาษา", "ใส่ชื่อสาขา", "หัวข้อ: {0}",
            "สร้าง", "ยกเลิก", "แก้ไข",
            "คลิกเพื่อเลือก", "คลิกเพื่อเลือก", "เลือกไอคอน", "ปิด",
            "ลบ", "แก้ไขสาขา", "บันทึก", "ลบสาขา?", "ลบสาขา \"{0}\"? ย้อนกลับไม่ได้", "ลบ",
            "ปิด",
            "เลือก", "ใหม่", "เส้น", "ลบ", "ตาราง",
            "เลือก", "เควสใหม่", "เชื่อม", "ลบ", "บันทึก", "รีเซ็ต", "แก้ไข",
            "ตาราง", "ออก", "สาขา", "เควส", "สร้าง", "แก้ไข", "ลบสาขา",
            "ไอคอน", "ตัวแก้ไข", "เริ่ม", "เควส", "จุดตรวจ", "ฆ่า",
            "ลากเควสเพื่อย้าย", "เลือกแล้ว #{0}", "คลิกเควสเพื่อแก้ไข", "คลิกกราฟเพื่อวาง",
            "เชื่อม: โหนดเริ่ม", "เชื่อม: โหนดจบ", "คลิกโหนดเพื่อลบ", "เลือกหรือสร้างโหนด",
            "กำลังแก้ไข #{0}", "เควส #{0}", "เลือกเครื่องมือ",
            "เลือกเควส", "เควส #{0}", "เป้าหมาย", "ฆ่า", "รางวัล", "ว่าง — กด +",
            "เลือก", "เลือกไอเท็ม", "ยกเลิก", "ค้นหาไอเท็ม…", "ไม่พบ",
            "ทุกประเภท", "ตรงทั้งหมด", "ใช้ไอเท็ม (ปิด = ตรวจอย่างเดียว)", "ใช้เมื่อรับ", "ตรวจอย่างเดียว",
            "คราฟต์", "มี", "ไอคอน: craft · ทั้งหมด  |  ใช้ — ด้านบน",
            "ใช้", "คราฟต์", "ทุกประเภท", "ฆ่า",
            "คลิกเพื่อเลือกสิ่งมีชีวิต", "เลือกสิ่งมีชีวิตที่จะฆ่า", "ค้นหาสิ่งมีชีวิต…", "ไม่พบ",
            "จำนวน", "บันทึก");
        Categories(d,
            "ตำนาน", "เส้นทาง EMBERSTAR",
            "ยุคหิน", "ยุคหิน", "เครื่องปั้นดินเผา", "เครื่องปั้นดินเผา",
            "ยุคทองแดง", "ยุคทองแดง", "ยุคสำริด", "ยุคสำริด",
            "ยุคเหล็ก", "ยุคเหล็ก", "ยุคเหล็กกล้า", "ยุคเหล็กกล้า",
            "ล่าสัตว์", "ล่าสัตว์", "เกษตรกรรม", "เกษตรกรรม",
            "หัตถกรรม", "หัตถกรรม", "การพเนจร", "การพเนจร",
            "สาขา {0}", "สาขา {0}");
        return d;
    }

    static Dictionary<string, string> Vi()
    {
        var d = Pack(
            "NHIỆM VỤ", "ĐÓNG", "Nhánh này chưa có nhiệm vụ.", "Nhật ký nhiệm vụ",
            "NHIỆM VỤ: ", "HOÀN THÀNH", "Mục tiêu:", "Phần thưởng:", "Văn bản thông tin",
            "BẮT ĐẦU ĐƯỜNG", "ĐÃ BẮT ĐẦU", "ĐANG XỬ LÝ...", "ĐÃ QUA", "TIẾP TỤC", "ĐÃ NHẬN", "NHẬN THƯỞNG",
            "Chế tạo", "Chế tạo & nộp", "Nộp", "Thu thập", "Giết",
            "Chế tạo vật phẩm", "Chế tạo và nộp vật phẩm", "Nộp vật phẩm để nhận thưởng",
            "Có vật phẩm (không bị lấy)", "Giết sinh vật", "Xem nhãn dưới mỗi vật phẩm",
            "Đã chế tạo: {0}/{1}",
            "THÊM PHẦN TỬ", "CHỌN PRESET", "Mục tiêu_ID {0}", "Phần thưởng_ID {0}", "(sl)", "Văn bản thông tin",
            "Hướng | ", "Preset | ", "Nút | ",
            "THÊM", "XÓA", "XÓA HẾT", "LƯU",
            "BẮT ĐẦU", "NHIỆM VỤ", "CHECKPOINT", "GIẾT", "TRÌNH SỬA",
            "NHÁNH", "NHIỆM VỤ",
            "Nhánh: {0}", "Chọn nhánh hoặc tạo mới", "CHỌN NHÁNH", "Nhánh \"{0}\"",
            "+ NHÁNH", "Nhánh nhiệm vụ mới", "Tên nhánh", "Ngôn ngữ", "Nhập tên nhánh", "Tiêu đề: {0}",
            "TẠO", "HỦY", "SỬA",
            "Nhấp để chọn", "Nhấp để chọn", "Chọn biểu tượng", "ĐÓNG",
            "XÓA", "Sửa nhánh", "LƯU", "Xóa nhánh?", "Xóa nhánh \"{0}\"? Không hoàn tác được.", "XÓA",
            "ĐÓNG",
            "CHỌN", "MỚI", "ĐƯỜNG", "XÓA", "LƯỚI",
            "CHỌN", "NV MỚI", "LIÊN KẾT", "XÓA", "LƯU", "ĐẶT LẠI", "SỬA",
            "LƯỚI", "THOÁT", "NHÁNH", "NHIỆM VỤ", "TẠO", "SỬA", "XÓA NHÁNH",
            "Biểu tượng", "Trình sửa", "BẮT ĐẦU", "NHIỆM VỤ", "CHECKP.", "GIẾT",
            "Kéo nhiệm vụ để di chuyển", "Đã chọn #{0}", "Nhấp nhiệm vụ để sửa", "Nhấp đồ thị để đặt",
            "Liên kết: nút đầu", "Liên kết: nút cuối", "Nhấp nút để xóa", "Chọn hoặc tạo nút",
            "Đang sửa #{0}", "Nhiệm vụ #{0}", "Chọn công cụ",
            "Chọn nhiệm vụ", "Nhiệm vụ #{0}", "Mục tiêu", "Giết", "Phần thưởng", "Trống — nhấn +",
            "Chọn", "Chọn vật phẩm", "Hủy", "Tìm vật phẩm…", "Không có kết quả",
            "Mọi loại", "Chính xác", "Tiêu thụ vật phẩm (tắt = chỉ phát hiện)", "Tiêu thụ khi nhận", "Chỉ phát hiện",
            "Chế tạo", "Sở hữu", "biểu tượng: craft · tất cả  |  tiêu thụ — trên",
            "Consume", "Craft", "Mọi loại", "Giết",
            "Nhấp để chọn sinh vật", "Chọn sinh vật để giết", "Tìm sinh vật…", "Không có kết quả",
            "SL", "LƯU");
        Categories(d,
            "Huyền thoại", "ĐƯỜNG EMBERSTAR",
            "Thời đồ đá", "THỜI ĐỒ ĐÁ", "Gốm", "GỐM",
            "Thời đồ đồng đỏ", "THỜI ĐỒ ĐỒNG ĐỎ", "Thời đồ đồng", "THỜI ĐỒ ĐỒNG",
            "Thời đồ sắt", "THỜI ĐỒ SẮT", "Thời thép", "THỜI THÉP",
            "Săn bắn", "SĂN BẮN", "Nông nghiệp", "NÔNG NGHIỆP",
            "Thủ công", "THỦ CÔNG", "Lang thang", "LANG THANG",
            "Nhánh {0}", "NHÁNH {0}");
        return d;
    }
}

