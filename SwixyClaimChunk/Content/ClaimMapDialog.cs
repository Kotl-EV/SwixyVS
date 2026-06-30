// =============================================================================
// ClaimMapDialog.cs
// -----------------------------------------------------------------------------
// Главное GUI-окно мода приватов: две вкладки — карта чанков и настройки приватов.
// Клиент отправляет пакеты через IClientNetworkChannel; ответы применяются через
// ApplyState / ApplyClaimList / ApplyClaimShow без полной пересборки, где возможно.
// Скролл списков сохраняется при обновлении; тяжёлые обновления откладываются через
// RunClaimsUiDeferred (один кадр), чтобы не сбрасывать позицию прокрутки.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyClaimChunk.Content;

/// <summary>
/// Диалог карты приватов и редактора списка приватов/участников.
/// </summary>
public sealed class ClaimMapDialog : GuiDialog
{
    #region Константы и поля состояния

    /// <summary>Радиус окна карты по умолчанию (в чанках от центра).</summary>
    private const int DefaultRadius = 10;

    /// <summary>Индекс вкладки «Карта чанков».</summary>
    private const int PageMap = 0;

    /// <summary>Индекс вкладки «Мои приваты».</summary>
    private const int PageClaims = 1;

    /// <summary>Клиентский API Vintage Story.</summary>
    private readonly ICoreClientAPI clientApi;

    /// <summary>Сетевой канал SwixyClaimChunk для пакетов карты и приватов.</summary>
    private readonly IClientNetworkChannel channel;

    /// <summary>Сетка чанков на вкладке карты; null до первой компоновки.</summary>
    private ClaimMapGridElement? gridElement;

    /// <summary>Последний снимок карты с сервера.</summary>
    private ClaimMapStatePacket? mapState;

    /// <summary>Кэш списка приватов игрока и сообщений UI.</summary>
    private ClaimListStatePacket? claimListState;

    /// <summary>Центр видимого окна карты по X (координата чанка).</summary>
    private int centerChunkX;

    /// <summary>Центр видимого окна карты по Z (координата чанка).</summary>
    private int centerChunkZ;

    /// <summary>Радиус окна карты в чанках (ограничивается сервером).</summary>
    private int radius = DefaultRadius;

    /// <summary>Активная вкладка: PageMap или PageClaims.</summary>
    private int activePage = PageMap;

    /// <summary>ClaimId выбранного привата на вкладке настроек (0 — нет выбора).</summary>
    private int selectedClaimId;

    /// <summary>Приват, подсвеченный в мире; 0 — подсветка выключена.</summary>
    private int highlightedClaimId;

    /// <summary>Ожидаемое состояние подсветки после клика (-1 — нет ожидания).</summary>
    private int pendingHighlightClaimId = -1;

    /// <summary>UID выбранного участника (для поля ввода и удаления).</summary>
    private string selectedMemberUid = "";

    /// <summary>Отображаемое имя выбранного участника.</summary>
    private string selectedMemberName = "";

    /// <summary>Текст в поле «добавить игрока».</summary>
    private string memberNameInput = "";

    /// <summary>Текст в поле переименования привата.</summary>
    private string claimNameInput = "";

    /// <summary>Сохранённая позиция скролла списка приватов (пиксели).</summary>
    private float claimListScrollValue;

    /// <summary>Сохранённая позиция скролла списка участников.</summary>
    private float memberListScrollValue;

    /// <summary>Границы таблицы списка приватов (для скролла).</summary>
    private ElementBounds? claimListTableBounds;

    /// <summary>Границы клип-области списка приватов.</summary>
    private ElementBounds? claimListClipBounds;

    /// <summary>Границы таблицы списка участников.</summary>
    private ElementBounds? memberListTableBounds;

    /// <summary>Границы клип-области списка участников.</summary>
    private ElementBounds? memberListClipBounds;

    /// <summary>Флаг: отложенное обновление UI уже запланировано на следующий тик.</summary>
    private bool claimsUiDeferScheduled;

    /// <summary>Действие для отложенного обновления UI (иконки, выбор без ComposeDialog).</summary>
    private Action? deferredClaimsUiAction;

    #endregion

    /// <summary>Код горячей клавиши P для открытия карты приватов.</summary>
    public override string ToggleKeyCombinationCode => SwixyClaimChunkMod.OpenMapHotkeyCode;

    /// <summary>Не захватывать мышь глобально — удобнее кликать по карте.</summary>
    public override bool PrefersUngrabbedMouse => true;

    #region Конструктор и жизненный цикл

    /// <summary>Создаёт диалог и центрирует карту на позиции игрока.</summary>
    public ClaimMapDialog(ICoreClientAPI capi, IClientNetworkChannel channel)
        : base(capi)
    {
        clientApi = capi;
        this.channel = channel;
        CenterOnPlayer();
        ComposeDialog();
    }

    #endregion

    #region Запросы к серверу и применение пакетов

    /// <summary>Запрашивает обновление карты чанков; при useMapView учитывает текущий viewport сетки.</summary>
    public void RequestRefresh(bool useMapView = false)
    {
        var requestCenterX = centerChunkX;
        var requestCenterZ = centerChunkZ;
        var requestRadius = radius;

        if (useMapView)
        {
            var request = gridElement?.GetVisibleRequest(centerChunkX, centerChunkZ, radius) ?? (centerChunkX, centerChunkZ, radius);
            requestCenterX = request.CenterChunkX;
            requestCenterZ = request.CenterChunkZ;
            requestRadius = request.Radius;
            centerChunkX = requestCenterX;
            centerChunkZ = requestCenterZ;
            radius = requestRadius;
        }

        clientApi.Logger.Notification(
            "[SwixyClaimChunk] Sending map request center={0},{1} radius={2}",
            requestCenterX,
            requestCenterZ,
            requestRadius);

        channel.SendPacket(new ClaimMapRequestPacket
        {
            CenterChunkX = requestCenterX,
            CenterChunkZ = requestCenterZ,
            Radius = requestRadius
        });
    }

    /// <summary>Применяет снимок карты с сервера к сетке и подписям квот.</summary>
    public void ApplyState(ClaimMapStatePacket packet)
    {
        mapState = packet;
        centerChunkX = packet.CenterChunkX;
        centerChunkZ = packet.CenterChunkZ;
        radius = packet.Radius;

        gridElement?.SetState(packet);
        UpdateHint(packet);
        UpdateText(packet);
    }

    /// <summary>
    /// Обновляет кэш списка приватов. Сохраняет скролл; по возможности обновляет вкладку без полной пересборки.
    /// </summary>
    public void ApplyClaimList(ClaimListStatePacket packet)
    {
        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();

        claimListState = packet;

        if (packet.Claims.Count == 0)
        {
            selectedClaimId = 0;
            selectedMemberUid = "";
            selectedMemberName = "";
        }
        else if (selectedClaimId == 0 || packet.Claims.All(claim => claim.ClaimId != selectedClaimId))
        {
            SelectClaim(packet.Claims[0]);
        }

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null || selectedClaim.Members.All(member => member.PlayerUid != selectedMemberUid))
        {
            selectedMemberUid = "";
            selectedMemberName = "";
        }

        if (highlightedClaimId > 0 && packet.Claims.All(claim => claim.ClaimId != highlightedClaimId))
        {
            highlightedClaimId = 0;
        }

        if (activePage == PageClaims)
        {
            if (packet.Claims.Count == 0 || !TryRefreshClaimsPageInPlace(savedClaimScroll, savedMemberScroll))
            {
                ComposeDialog();
                RestoreClaimListScroll(savedClaimScroll);
                RestoreMemberListScroll(savedMemberScroll);
                ApplyClaimsPageInputState();
            }

            SingleComposer?.GetDynamicText("claimsMessage")?.SetNewText(packet.Message ?? "");
        }
    }

    /// <summary>Синхронизирует состояние подсветки привата в мире с ответом сервера.</summary>
    public void ApplyClaimShow(ClaimShowStatePacket packet)
    {
        ApplyHighlightStateFromServer(packet.Active, packet.ClaimId);

        if (activePage == PageClaims)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    /// <summary>При открытии — центр на игроке, запрос карты и списка приватов.</summary>
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        CenterOnPlayer();
        gridElement?.CenterMapOnPlayer();
        RequestRefresh();
        RequestClaimList();

        if (highlightedClaimId > 0 && activePage == PageClaims)
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(GetClaimListScrollOffset()));
        }
    }

    /// <summary>Escape закрывает диалог.</summary>
    public override bool OnEscapePressed()
    {
        TryClose();
        return true;
    }

    #endregion

    #region Компоновка GUI

    /// <summary>Полная пересборка диалога (обе вкладки) и восстановление скролла/полей ввода.</summary>
    private void ComposeDialog()
    {
        var mainBounds = ElementBounds.Fixed(0, 0, 780, 690);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(mainBounds);

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        gridElement?.Dispose();
        gridElement = null;
        if (activePage == PageMap)
        {
            gridElement = new ClaimMapGridElement(clientApi, ElementBounds.Fixed(18, 102, 540, 540), OnChunksSelected, () => RequestRefresh(useMapView: true));
        }

        ClearComposers();
        var composer = clientApi.Gui
            .CreateCompo("epclaimmap", dialogBounds)
            .AddShadedDialogBG(bgBounds, true)
            .AddDialogTitleBar(Lang.Get("swixyclaimchunk:claim-map-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .AddButton(Lang.Get("swixyclaimchunk:claim-map-tab-map"), SwitchToMapPage, ElementBounds.Fixed(18, 40, 110, 30), activePage == PageMap ? EnumButtonStyle.Small : EnumButtonStyle.Normal, "mapTab")
            .AddButton(Lang.Get("swixyclaimchunk:claim-map-tab-claims"), SwitchToClaimsPage, ElementBounds.Fixed(136, 40, 130, 30), activePage != PageMap ? EnumButtonStyle.Small : EnumButtonStyle.Normal, "claimsTab");

        if (activePage == PageMap)
        {
            ComposeMapPage(composer);
        }
        else
        {
            ComposeClaimsPage(composer);
        }

        SingleComposer = composer.EndChildElements().Compose();
        ApplyClaimsPageScrollState();
        ApplyClaimsPageInputState();
        UpdateText(null);
    }

    /// <summary>Вкладка карты: сетка чанков, легенда, квоты, кнопка «Центр».</summary>
    private void ComposeMapPage(GuiComposer composer)
    {
        composer
            .AddDynamicText(Lang.Get("swixyclaimchunk:claim-map-hint"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(18, 82, 520, 18), "hintLabel")
            .AddDynamicCustomDraw(ElementBounds.Fixed(571, 102, 175, 540), DrawSidePanelBackground, "sidePanelBg");

        if (gridElement != null)
        {
            composer.AddInteractiveElement(gridElement, "chunkGrid");
        }

        composer
            .AddDynamicText(Lang.Get("swixyclaimchunk:claim-map-used"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(582, 108, 150, 20), "usedLabel")
            .AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(582, 136, 150, 54), "usedText")
            .AddDynamicText(Lang.Get("swixyclaimchunk:claim-map-legend-title"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(582, 208, 150, 20), "legendTitle")
            .AddDynamicCustomDraw(ElementBounds.Fixed(582, 240, 150, 88), DrawLegend, "legend")
            .AddDynamicText("", CairoFont.WhiteSmallText(), ElementBounds.Fixed(582, 380, 150, 88), "messageText")
            .AddButton(Lang.Get("swixyclaimchunk:claim-map-center"), CenterButton, ElementBounds.Fixed(582, 596, 150, 34), EnumButtonStyle.Normal, "centerButton");
    }

    /// <summary>
    /// Вкладка приватов: список слева (x=20), справа — переименование, добавление игрока, участники.
    /// detailsX/detailsW задают правую панель; labelW/buttonW — сетка подпись+поле+кнопка.
    /// </summary>
    private void ComposeClaimsPage(GuiComposer composer)
    {
        const int detailsX = 304;
        const int detailsW = 422;

        composer
            .AddDynamicCustomDraw(ElementBounds.Fixed(18, 84, 258, 570), DrawSidePanelBackground, "claimListBg")
            .AddDynamicCustomDraw(ElementBounds.Fixed(292, 84, 454, 570), DrawSidePanelBackground, "claimDetailsBg")
            .AddDynamicText(Lang.Get("swixyclaimchunk:claims-list-title"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(20, 92, 224, 20), "claimsListTitle");

        var claims = claimListState?.Claims ?? [];
        var claimListBounds = ElementBounds.Fixed(20, 116, 224, 528);
        claimListClipBounds = claimListBounds.ForkContainingChild(3, 3, 3, 3);
        claimListTableBounds = claimListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        composer
            .AddInset(claimListBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnClaimListScroll, ElementStdBounds.VerticalScrollbar(claimListBounds), "claimListScroll")
            .BeginClip(claimListClipBounds)
            .AddCellList(claimListTableBounds, CreateClaimListCell, BuildClaimCells(), "claimList")
            .EndClip();

        if (claims.Count == 0)
        {
            composer.AddStaticText(Lang.Get("swixyclaimchunk:claims-empty"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(20, 118, 224, 40), "claimsEmpty");
        }

        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null)
        {
            composer.AddStaticText(Lang.Get("swixyclaimchunk:claims-select"), CairoFont.WhiteDetailText(), ElementBounds.Fixed(detailsX, 104, detailsW, 40), "claimSelectHint");
            return;
        }

        const int labelW = 124;
        const int buttonW = 122;
        const int controlH = 32;
        const int labelGap = 2;
        const int buttonGap = 8;
        const int renameInputX = detailsX + labelW + labelGap;
        const int buttonX = detailsX + detailsW - buttonW;
        const int renameInputW = buttonX - renameInputX - buttonGap;
        const int memberInputX = detailsX + labelW + labelGap;
        const int memberInputW = buttonX - memberInputX - buttonGap;
        const int memberListX = detailsX - 10;
        var actionButtonFont = CairoFont.ButtonText().WithFontSize(13);

        composer
            .AddDynamicText(
                Lang.Get("swixyclaimchunk:claims-stats", selectedClaim.AreaCount, selectedClaim.ChunkCount),
                CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(detailsX, 96, detailsW, 20),
                "claimStats")
            .AddDynamicCustomDraw(ElementBounds.Fixed(renameInputX, 116, renameInputW, controlH), DrawTextInputBackground, "claimNameInputBg")
            .AddTextInput(ElementBounds.Fixed(renameInputX + 4, 120, renameInputW - 8, 24), text => claimNameInput = text, CairoFont.WhiteDetailText(), "claimNameInput")
            .AddButton(Lang.Get("swixyclaimchunk:claims-rename-button"), RenameClaimButton, ElementBounds.Fixed(buttonX, 116, buttonW, controlH), actionButtonFont, EnumButtonStyle.Small, "renameClaim")
            .AddDynamicText(Lang.Get("swixyclaimchunk:claims-rename"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(detailsX, 120, labelW, 20), "renameLabel")
            .AddDynamicText(Lang.Get("swixyclaimchunk:claims-player-name"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(detailsX, 158, labelW, 20), "memberNameLabel")
            .AddDynamicCustomDraw(ElementBounds.Fixed(memberInputX, 154, memberInputW, controlH), DrawTextInputBackground, "memberNameInputBg")
            .AddTextInput(ElementBounds.Fixed(memberInputX + 4, 158, memberInputW - 8, 24), text => memberNameInput = text, CairoFont.WhiteDetailText(), "memberNameInput")
            .AddButton(Lang.Get("swixyclaimchunk:claims-add-player"), AddMemberButton, ElementBounds.Fixed(buttonX, 154, buttonW, controlH), actionButtonFont, EnumButtonStyle.Small, "addMember");

        var memberListBounds = ElementBounds.Fixed(memberListX, 196, detailsW, 408);
        memberListClipBounds = memberListBounds.ForkContainingChild(3, 3, 3, 3);
        memberListTableBounds = memberListClipBounds.ForkContainingChild(0, 0, 0, -3).WithFixedPadding(3);

        composer
            .AddDynamicCustomDraw(memberListBounds, DrawScrollAreaBackground, "memberListScrollBg")
            .AddInset(memberListBounds, 3, 0.85f)
            .AddVerticalScrollbar(OnMemberListScroll, ElementStdBounds.VerticalScrollbar(memberListBounds), "memberListScroll")
            .BeginClip(memberListClipBounds)
            .AddCellList(memberListTableBounds, CreateMemberCell, BuildMemberCells(selectedClaim), "memberList")
            .EndClip()
            .AddDynamicText(claimListState?.Message ?? "", CairoFont.WhiteSmallText(), ElementBounds.Fixed(detailsX, 612, detailsW, 40), "claimsMessage");
    }

    #endregion

    #region Карта — действия с чанками

    /// <summary>Отправляет пакет пакетного клейма/анклейма по выделенным чанкам сетки.</summary>
    private void OnChunksSelected(IReadOnlyList<(int ChunkX, int ChunkZ)> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var request = gridElement?.GetVisibleRequest(centerChunkX, centerChunkZ, radius) ?? (centerChunkX, centerChunkZ, radius);
        centerChunkX = request.CenterChunkX;
        centerChunkZ = request.CenterChunkZ;
        radius = request.Radius;

        SingleComposer?.GetDynamicText("messageText").SetNewText(Lang.Get("swixyclaimchunk:claim-map-working"));

        try
        {
            channel.SendPacket(new ClaimChunksBatchActionPacket
            {
                Chunks = chunks.Select(chunk => new ClaimChunkCoordPacket
                {
                    ChunkX = chunk.ChunkX,
                    ChunkZ = chunk.ChunkZ
                }).ToList(),
                CenterChunkX = centerChunkX,
                CenterChunkZ = centerChunkZ,
                Radius = radius
            });
            clientApi.Logger.Notification(
                "[SwixyClaimChunk] Sent ClaimChunksBatchActionPacket chunks={0} center={1},{2} radius={3}",
                chunks.Count,
                centerChunkX,
                centerChunkZ,
                radius);
        }
        catch (Exception exception)
        {
            clientApi.Logger.Error("Failed to send claim batch packet: {0}", exception);
            SingleComposer?.GetDynamicText("messageText").SetNewText(Lang.Get("swixyclaimchunk:error-send-request-failed"));
        }
    }

    #endregion

    #region Вкладки и запрос списка приватов

    /// <summary>Запрашивает у сервера актуальный список приватов игрока.</summary>
    private void RequestClaimList()
    {
        channel.SendPacket(new ClaimListRequestPacket());
    }

    /// <summary>Переключает на вкладку карты и пересобирает диалог.</summary>
    private bool SwitchToMapPage()
    {
        activePage = PageMap;
        ComposeDialog();
        if (mapState != null)
        {
            gridElement?.SetState(mapState);
        }

        return true;
    }

    /// <summary>Переключает на вкладку приватов, запрашивает список и пересобирает диалог.</summary>
    private bool SwitchToClaimsPage()
    {
        activePage = PageClaims;
        RequestClaimList();
        ComposeDialog();
        return true;
    }

    #endregion

    #region Выбор привата и участников

    /// <summary>Выбирает приват в списке; обновляет UI без полной пересборки, если возможно.</summary>
    private bool SelectClaimButton(ClaimInfoPacket claim)
    {
        if (selectedClaimId == claim.ClaimId && activePage == PageClaims)
        {
            return true;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        SelectClaim(claim);
        RunClaimsUiDeferred(() => RefreshClaimsSelectionUi(savedClaimScroll, 0));
        return true;
    }

    /// <summary>Открывает редактор привата (то же, что выбор в списке).</summary>
    private bool OpenClaimEditorButton(ClaimInfoPacket claim)
    {
        return SelectClaimButton(claim);
    }

    /// <summary>Устанавливает выбранный приват и сбрасывает выбор участника.</summary>
    private void SelectClaim(ClaimInfoPacket claim)
    {
        selectedClaimId = claim.ClaimId;
        selectedMemberUid = "";
        selectedMemberName = "";
        claimNameInput = claim.Name;
    }

    /// <summary>Выбирает участника; обновляет правую панель без полной пересборки.</summary>
    private bool SelectMemberButton(ClaimMemberPacket member)
    {
        if (selectedMemberUid == member.PlayerUid && activePage == PageClaims)
        {
            return true;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        SelectMember(member);

        RunClaimsUiDeferred(() => RefreshMemberSelectionUi(savedClaimScroll, savedMemberScroll));
        return true;
    }

    /// <summary>Запоминает UID/ник участника; для не-владельца подставляет ник в поле добавления.</summary>
    private void SelectMember(ClaimMemberPacket member)
    {
        selectedMemberUid = member.PlayerUid;
        selectedMemberName = member.PlayerName;
        if (!member.IsOwner)
        {
            memberNameInput = member.PlayerName;
        }
    }

    #endregion

    #region Действия с приватами и участниками (сеть)

    /// <summary>Добавляет игрока из поля ввода с правами Use+Build.</summary>
    private bool AddMemberButton()
    {
        memberNameInput = SingleComposer?.GetTextInput("memberNameInput")?.GetText() ?? memberNameInput;
        SendClaimAction(
            ClaimAccessActionType.AddPlayer,
            memberNameInput,
            (int)(EnumBlockAccessFlags.Use | EnumBlockAccessFlags.BuildOrBreak),
            "");
        return true;
    }

    /// <summary>Удаляет участника по UID; владельца удалить нельзя.</summary>
    private void RemoveMemberByUid(string memberUid)
    {
        var member = FindMemberByUid(memberUid);
        if (member == null || member.IsOwner)
        {
            return;
        }

        SendClaimAction(ClaimAccessActionType.RemovePlayer, member.PlayerName, 0, "", member.PlayerUid);
    }

    /// <summary>Переключает статус со-владельца участника (корона).</summary>
    private void GrantCoOwnershipByUid(string memberUid)
    {
        var claim = GetSelectedClaim();
        var member = FindMemberByUid(memberUid);
        if (claim == null || member == null || member.IsOwner || claim.ViewerIsCoOwner)
        {
            return;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        member.IsCoOwner = !member.IsCoOwner;
        member.AccessName = FormatMemberAccessName(member.AccessFlags, member.IsOwner, member.IsCoOwner);
        RefreshMemberAccessIcons(savedClaimScroll, savedMemberScroll);
        SendClaimAction(ClaimAccessActionType.GrantCoOwnership, member.PlayerName, 0, "", member.PlayerUid);
    }

    private bool RenameClaimButton()
    {
        claimNameInput = SingleComposer?.GetTextInput("claimNameInput")?.GetText() ?? claimNameInput;
        SendClaimAction(ClaimAccessActionType.RenameClaim, "", 0, claimNameInput);
        return true;
    }

    /// <summary>Отправляет ClaimAccessActionPacket для выбранного привата.</summary>
    private void SendClaimAction(int action, string playerName, int accessFlags, string claimName, string playerUid = "")
    {
        if (selectedClaimId <= 0)
        {
            return;
        }

        channel.SendPacket(new ClaimAccessActionPacket
        {
            ClaimId = selectedClaimId,
            Action = action,
            PlayerName = playerName,
            PlayerUid = playerUid,
            AccessFlags = accessFlags,
            ClaimName = claimName
        });
    }

    #endregion

    #region Ячейки списков приватов и участников

    /// <summary>Фабрика ячейки списка приватов с колбэками клика/подсветки/удаления.</summary>
    private IGuiElementCell CreateClaimListCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var claim = FindClaimForCell(cell);
        var element = new ClaimHighlightListCell(clientApi, cell, bounds, claim?.ClaimId == highlightedClaimId)
        {
            AllowDelete = claim is { ViewerIsCoOwner: false },
            OnMouseDownOnCellLeft = SelectClaimCell,
            OnMouseDownOnCellRight = ToggleClaimHighlightCell,
            OnMouseDownOnCellDelete = DeleteClaimCell
        };
        return element;
    }

    /// <summary>Фабрика ячейки участника с переключателями Use/Build и удалением.</summary>
    private IGuiElementCell CreateMemberCell(SavegameCellEntry cell, ElementBounds bounds)
    {
        var member = FindMemberForCell(cell);
        var flags = (EnumBlockAccessFlags)(member?.AccessFlags ?? 0);
        var element = new ClaimMemberListCell(
            clientApi,
            cell,
            bounds,
            flags.HasFlag(EnumBlockAccessFlags.Use),
            flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak),
            member?.IsOwner ?? false)
        {
            MemberUid = member?.PlayerUid ?? "",
            IsCoOwner = member?.IsCoOwner ?? false,
            AllowCoOwnerCrown = GetSelectedClaim() is { ViewerIsCoOwner: false },
            OnMouseDownOnCellLeft = SelectMemberCell,
            OnMakeOwner = GrantCoOwnershipByUid,
            OnToggleUse = ToggleMemberUseByUid,
            OnToggleBuild = ToggleMemberBuildByUid,
            OnDeleteMember = RemoveMemberByUid
        };
        return element;
    }

    /// <summary>Подпись строки привата в левом списке.</summary>
    private string BuildClaimListDetailText(ClaimInfoPacket claim)
    {
        return claim.ViewerIsCoOwner
            ? Lang.Get("swixyclaimchunk:claims-list-coowner-stats", claim.OwnerName, claim.ChunkCount)
            : Lang.Get("swixyclaimchunk:claims-list-stats", claim.ChunkCount);
    }

    /// <summary>Строит данные строк списка приватов из claimListState.</summary>
    private IEnumerable<SavegameCellEntry> BuildClaimCells()
    {
        foreach (var claim in claimListState?.Claims ?? [])
        {
            yield return new SavegameCellEntry
            {
                Title = claim.Name,
                DetailText = BuildClaimListDetailText(claim),
                LeftOffY = 2,
                DetailTextOffY = 2,
                HoverText = Lang.Get("swixyclaimchunk:claims-highlight-hint"),
                Selected = claim.ClaimId == selectedClaimId,
                Enabled = true,
                DrawAsButton = true
            };
        }
    }

    /// <summary>Строит данные строк списка участников выбранного привата.</summary>
    private IEnumerable<SavegameCellEntry> BuildMemberCells(ClaimInfoPacket selectedClaim)
    {
        foreach (var member in selectedClaim.Members ?? [])
        {
            yield return new SavegameCellEntry
            {
                Title = member.PlayerName,
                DetailText = "",
                Selected = false,
                Enabled = true,
                DrawAsButton = true
            };
        }
    }

    /// <summary>Находит участника по заголовку ячейки (ник игрока).</summary>
    private ClaimMemberPacket? FindMemberForCell(SavegameCellEntry cell)
    {
        foreach (var member in GetSelectedClaim()?.Members ?? [])
        {
            if (member.PlayerName == cell.Title)
            {
                return member;
            }
        }

        return null;
    }

    /// <summary>Возвращает выбранного участника по selectedMemberUid.</summary>
    private ClaimMemberPacket? GetSelectedMember()
    {
        return GetSelectedClaim()?.Members.FirstOrDefault(member => member.PlayerUid == selectedMemberUid);
    }

    #endregion

    #region Обработчики кликов по ячейкам

    /// <summary>Клик по строке привата — выбор привата.</summary>
    private void SelectClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null)
        {
            return;
        }

        SelectClaimButton(claim);
    }

    /// <summary>Удаление привата; при активной подсветке сначала снимает её на сервере.</summary>
    private void DeleteClaimCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null || claim.ViewerIsCoOwner)
        {
            return;
        }

        SelectClaim(claim);

        if (highlightedClaimId == claim.ClaimId)
        {
            channel.SendPacket(new ClaimShowRequestPacket
            {
                ClaimId = claim.ClaimId,
                Clear = true
            });
            highlightedClaimId = 0;
            pendingHighlightClaimId = -1;
        }

        SendClaimAction(ClaimAccessActionType.DeleteClaim, "", 0, "");
    }

    /// <summary>Переключает право Use у участника (оптимистичное обновление UI).</summary>
    private void ToggleMemberUseByUid(string memberUid)
    {
        ToggleMemberAccessByUid(memberUid, EnumBlockAccessFlags.Use);
    }

    /// <summary>Переключает право Build у участника (оптимистичное обновление UI).</summary>
    private void ToggleMemberBuildByUid(string memberUid)
    {
        ToggleMemberAccessByUid(memberUid, EnumBlockAccessFlags.BuildOrBreak);
    }

    /// <summary>Инвертирует флаг доступа и отправляет UpdateMemberAccess на сервер.</summary>
    private void ToggleMemberAccessByUid(string memberUid, EnumBlockAccessFlags flag)
    {
        var member = FindMemberByUid(memberUid);
        if (member == null || member.IsOwner)
        {
            return;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var savedMemberScroll = GetMemberListScrollOffset();
        var flags = (EnumBlockAccessFlags)member.AccessFlags;
        member.AccessFlags = flags.HasFlag(flag)
            ? (int)(flags & ~flag)
            : (int)(flags | flag);
        member.AccessName = FormatMemberAccessName(member.AccessFlags, member.IsOwner, member.IsCoOwner);
        RefreshMemberAccessIcons(savedClaimScroll, savedMemberScroll);
        SendClaimAction(ClaimAccessActionType.UpdateMemberAccess, member.PlayerName, member.AccessFlags, "", member.PlayerUid);
    }

    /// <summary>Ищет участника в выбранном привате по UID.</summary>
    private ClaimMemberPacket? FindMemberByUid(string memberUid)
    {
        return GetSelectedClaim()?.Members.FirstOrDefault(member => member.PlayerUid == memberUid);
    }

    /// <summary>Клик по лампочке — вкл/выкл подсветку привата в мире.</summary>
    private void ToggleClaimHighlightCell(int index)
    {
        var claim = GetClaimAt(index);
        if (claim == null)
        {
            return;
        }

        var savedClaimScroll = GetClaimListScrollOffset();
        var claimChanged = selectedClaimId != claim.ClaimId;
        var turningOff = highlightedClaimId == claim.ClaimId;

        SelectClaim(claim);
        SetPendingHighlightState(turningOff ? 0 : claim.ClaimId);

        channel.SendPacket(new ClaimShowRequestPacket
        {
            ClaimId = claim.ClaimId,
            Clear = turningOff
        });

        if (claimChanged)
        {
            RunClaimsUiDeferred(() => RefreshClaimsSelectionUi(savedClaimScroll, 0));
        }
        else
        {
            RunClaimsUiDeferred(() => RefreshClaimHighlightIcons(savedClaimScroll));
        }
    }

    /// <summary>Сопоставляет ячейку списка с ClaimInfoPacket по имени и статистике.</summary>
    private ClaimInfoPacket? FindClaimForCell(SavegameCellEntry cell)
    {
        foreach (var claim in claimListState?.Claims ?? [])
        {
            if (claim.Name == cell.Title
                && BuildClaimListDetailText(claim) == cell.DetailText)
            {
                return claim;
            }
        }

        return null;
    }

    #endregion

    #region Подсветка приватов в мире

    /// <summary>Локально выставляет ожидаемое состояние подсветки до ответа сервера.</summary>
    private void SetPendingHighlightState(int claimId)
    {
        pendingHighlightClaimId = claimId;
        highlightedClaimId = claimId;
    }

    /// <summary>Применяет подтверждённое сервером состояние подсветки; учитывает pending-флаг.</summary>
    private void ApplyHighlightStateFromServer(bool active, int claimId)
    {
        if (active)
        {
            if (pendingHighlightClaimId < 0 || pendingHighlightClaimId == claimId)
            {
                highlightedClaimId = claimId;
                pendingHighlightClaimId = -1;
            }

            return;
        }

        if (pendingHighlightClaimId > 0)
        {
            return;
        }

        if (pendingHighlightClaimId == 0 || pendingHighlightClaimId < 0)
        {
            highlightedClaimId = 0;
            pendingHighlightClaimId = -1;
        }
    }

    /// <summary>Обновляет иконки лампочек в списке приватов без перезагрузки ячеек.</summary>
    private void RefreshClaimHighlightIcons(float savedScroll)
    {
        RefreshClaimListSelection(savedScroll, reloadCells: false);
    }

    /// <summary>Обновляет иконки Use/Build в списке участников и восстанавливает скролл.</summary>
    private void RefreshMemberAccessIcons(float savedClaimScroll, float savedMemberScroll)
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        UpdateMemberListIconsInPlace(cellList);
        RestoreClaimListScroll(savedClaimScroll);
        RestoreMemberListScroll(savedMemberScroll);
    }

    /// <summary>Перекомпоновывает существующие ClaimMemberListCell по данным из кэша.</summary>
    private void UpdateMemberListIconsInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not ClaimMemberListCell memberCell)
            {
                continue;
            }

            var member = GetMemberAt(i);
            if (member == null)
            {
                continue;
            }

            var flags = (EnumBlockAccessFlags)member.AccessFlags;
            memberCell.MemberUid = member.PlayerUid;
            memberCell.AccessUse = flags.HasFlag(EnumBlockAccessFlags.Use);
            memberCell.AccessBuild = flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak);
            memberCell.IsOwner = member.IsOwner;
            memberCell.IsCoOwner = member.IsCoOwner;
            memberCell.AllowCoOwnerCrown = !(GetSelectedClaim()?.ViewerIsCoOwner ?? true);
            memberCell.Compose();
        }
    }

    /// <summary>Формирует локализованную строку прав доступа (Use, Build или «нет»).</summary>
    private static string FormatMemberAccessName(int accessFlags, bool isOwner = false, bool isCoOwner = false)
    {
        if (isOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-owner-role");
        }

        if (isCoOwner)
        {
            return Lang.Get("swixyclaimchunk:claims-coowner-role");
        }

        var flags = (EnumBlockAccessFlags)accessFlags;
        var parts = new List<string>();
        if (flags.HasFlag(EnumBlockAccessFlags.Use))
        {
            parts.Add(Lang.Get("swixyclaimchunk:claims-access-use"));
        }

        if (flags.HasFlag(EnumBlockAccessFlags.BuildOrBreak))
        {
            parts.Add(Lang.Get("swixyclaimchunk:claims-access-build"));
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : Lang.Get("swixyclaimchunk:claims-access-none");
    }

    /// <summary>Клик по строке участника — выбор участника.</summary>
    private void SelectMemberCell(int index)
    {
        var member = GetMemberAt(index);
        if (member == null)
        {
            return;
        }

        SelectMemberButton(member);
    }

    #endregion

    #region Доступ к данным списков

    /// <summary>Возвращает пакет выбранного привата или null.</summary>
    private ClaimInfoPacket? GetSelectedClaim()
    {
        return claimListState?.Claims.FirstOrDefault(claim => claim.ClaimId == selectedClaimId);
    }

    /// <summary>Возвращает приват по индексу в claimListState.Claims.</summary>
    private ClaimInfoPacket? GetClaimAt(int index)
    {
        var claims = claimListState?.Claims;
        if (claims == null || index < 0 || index >= claims.Count)
        {
            return null;
        }

        return claims[index];
    }

    /// <summary>Возвращает участника по индексу в Members выбранного привата.</summary>
    private ClaimMemberPacket? GetMemberAt(int index)
    {
        var members = GetSelectedClaim()?.Members;
        if (members == null || index < 0 || index >= members.Count)
        {
            return null;
        }

        return members[index];
    }

    #endregion

    #region Скролл списков и отложенное обновление UI

    /// <summary>Обработчик скроллбара списка приватов — сдвигает fixedY таблицы.</summary>
    private void OnClaimListScroll(float value)
    {
        claimListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>
    /// Планирует обновление UI на следующий тик (0 мс). Объединяет несколько вызовов за кадр;
    /// не выполняется, если диалог закрыт или активна вкладка карты.
    /// </summary>
    private void RunClaimsUiDeferred(Action action)
    {
        deferredClaimsUiAction = action;
        if (claimsUiDeferScheduled)
        {
            return;
        }

        claimsUiDeferScheduled = true;
        // RegisterCallback(0) — один кадр задержки, чтобы не сбрасывать скролл при серии кликов
        clientApi.Event.RegisterCallback(_ =>
        {
            claimsUiDeferScheduled = false;
            var deferredAction = deferredClaimsUiAction;
            deferredClaimsUiAction = null;

            if (!IsOpened() || activePage != PageClaims)
            {
                return;
            }

            deferredAction?.Invoke();
        }, 0);
    }

    /// <summary>Обновляет выбор привата: in-place или полная пересборка с восстановлением скролла.</summary>
    private void RefreshClaimsSelectionUi(float savedClaimScroll, float savedMemberScroll)
    {
        if (!TryRefreshClaimsPageInPlace(savedClaimScroll, savedMemberScroll, reloadClaimCells: false))
        {
            ComposeDialog();
            RestoreClaimListScroll(savedClaimScroll);
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

    /// <summary>Обновляет выбор участника: перезагрузка списка участников или ComposeDialog.</summary>
    private void RefreshMemberSelectionUi(float savedClaimScroll, float savedMemberScroll)
    {
        if (!TryRefreshMemberListInPlace(savedClaimScroll, savedMemberScroll))
        {
            ComposeDialog();
            RestoreClaimListScroll(savedClaimScroll);
            RestoreMemberListScroll(savedMemberScroll);
            ApplyClaimsPageInputState();
        }
    }

    /// <summary>Пытается обновить вкладку приватов без ComposeDialog (список, детали, скролл).</summary>
    private bool TryRefreshClaimsPageInPlace(float savedClaimScroll, float savedMemberScroll, bool reloadClaimCells = true)
    {
        if (activePage != PageClaims || SingleComposer == null || GetSelectedClaim() == null)
        {
            return false;
        }

        if (SingleComposer.GetTextInput("claimNameInput") == null)
        {
            return false;
        }

        RefreshClaimListSelection(savedClaimScroll, reloadClaimCells);
        RefreshClaimDetailsUi();
        RestoreMemberListScroll(savedMemberScroll);
        return true;
    }

    /// <summary>Перезагружает только список участников и восстанавливает оба скролла.</summary>
    private bool TryRefreshMemberListInPlace(float savedClaimScroll, float savedMemberScroll)
    {
        if (activePage != PageClaims || SingleComposer == null || GetSelectedClaim() == null)
        {
            return false;
        }

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList == null)
        {
            return false;
        }

        memberList.ReloadCells(BuildMemberCells(GetSelectedClaim()!));
        RestoreClaimListScroll(savedClaimScroll);
        RestoreMemberListScroll(savedMemberScroll);
        ApplyClaimsPageInputState();
        return true;
    }

    /// <summary>Обновляет выделение/лампочки в списке приватов; опционально ReloadCells.</summary>
    private void RefreshClaimListSelection(float savedScroll, bool reloadCells)
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        if (reloadCells)
        {
            cellList.ReloadCells(BuildClaimCells());
        }
        else
        {
            UpdateClaimListSelectionInPlace(cellList);
        }

        RestoreClaimListScroll(savedScroll);
    }

    /// <summary>Обновляет Selected и HighlightActive у существующих ClaimHighlightListCell.</summary>
    private void UpdateClaimListSelectionInPlace(GuiElementCellList<SavegameCellEntry> cellList)
    {
        for (var i = 0; i < cellList.elementCells.Count; i++)
        {
            if (cellList.elementCells[i] is not ClaimHighlightListCell highlightCell)
            {
                continue;
            }

            var claim = GetClaimAt(i);
            if (claim == null)
            {
                continue;
            }

            highlightCell.cellEntry.Selected = claim.ClaimId == selectedClaimId;
            highlightCell.HighlightActive = claim.ClaimId == highlightedClaimId;
            highlightCell.AllowDelete = !claim.ViewerIsCoOwner;
            highlightCell.Compose();
        }
    }

    /// <summary>Обновляет статистику, поле имени и список участников правой панели.</summary>
    private void RefreshClaimDetailsUi()
    {
        var selectedClaim = GetSelectedClaim();
        if (selectedClaim == null || SingleComposer == null)
        {
            return;
        }

        claimNameInput = selectedClaim.Name;
        SingleComposer.GetDynamicText("claimStats")?.SetNewText(
            selectedClaim.ViewerIsCoOwner
                ? Lang.Get("swixyclaimchunk:claims-stats-coowner", selectedClaim.OwnerName, selectedClaim.AreaCount, selectedClaim.ChunkCount)
                : Lang.Get("swixyclaimchunk:claims-stats", selectedClaim.AreaCount, selectedClaim.ChunkCount));

        var memberList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (memberList != null)
        {
            memberList.ReloadCells(BuildMemberCells(selectedClaim));
            RestoreMemberListScroll(0);
        }

        ApplyClaimsPageInputState();
    }

    /// <summary>Текущий сдвиг скролла списка приватов (из fixedY или кэша).</summary>
    private float GetClaimListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("claimList");
        if (cellList != null)
        {
            return (float)System.Math.Max(0, -cellList.Bounds.fixedY);
        }

        return claimListScrollValue;
    }

    /// <summary>Текущий сдвиг скролла списка участников.</summary>
    private float GetMemberListScrollOffset()
    {
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList != null)
        {
            return (float)System.Math.Max(0, -cellList.Bounds.fixedY);
        }

        return memberListScrollValue;
    }

    /// <summary>Обработчик скроллбара списка участников.</summary>
    private void OnMemberListScroll(float value)
    {
        memberListScrollValue = value;
        var cellList = SingleComposer?.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        cellList.Bounds.fixedY = -value;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>Восстанавливает сохранённые позиции скролла после ComposeDialog.</summary>
    private void ApplyClaimsPageScrollState()
    {
        if (activePage == PageMap || SingleComposer == null)
        {
            return;
        }

        RestoreClaimListScroll(claimListScrollValue);
        RestoreMemberListScroll(memberListScrollValue);
    }

    /// <summary>Пересчитывает высоты, ограничивает scrollValue и синхронизирует скроллбар с таблицей.</summary>
    private void RestoreClaimListScroll(float scrollValue)
    {
        if (SingleComposer == null || claimListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("claimList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        claimListClipBounds.CalcWorldBounds();

        var clipHeight = (float)claimListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = System.Math.Max(0, tableHeight - clipHeight);
        claimListScrollValue = System.Math.Clamp(scrollValue, 0, maxScroll);

        var claimScroll = SingleComposer.GetScrollbar("claimListScroll");
        if (claimScroll != null)
        {
            claimScroll.SetHeights(clipHeight, tableHeight);
            claimScroll.CurrentYPosition = claimListScrollValue;
            claimScroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -claimListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>Аналог RestoreClaimListScroll для списка участников.</summary>
    private void RestoreMemberListScroll(float scrollValue)
    {
        if (SingleComposer == null || memberListClipBounds == null)
        {
            return;
        }

        var cellList = SingleComposer.GetCellList<SavegameCellEntry>("memberList");
        if (cellList == null)
        {
            return;
        }

        cellList.CalcTotalHeight();
        cellList.Bounds.CalcWorldBounds();
        memberListClipBounds.CalcWorldBounds();

        var clipHeight = (float)memberListClipBounds.fixedHeight;
        var tableHeight = (float)cellList.Bounds.fixedHeight;
        var maxScroll = System.Math.Max(0, tableHeight - clipHeight);
        memberListScrollValue = System.Math.Clamp(scrollValue, 0, maxScroll);

        var memberScroll = SingleComposer.GetScrollbar("memberListScroll");
        if (memberScroll != null)
        {
            memberScroll.SetHeights(clipHeight, tableHeight);
            memberScroll.CurrentYPosition = memberListScrollValue;
            memberScroll.RecomposeHandle();
        }

        cellList.Bounds.fixedY = -memberListScrollValue;
        cellList.Bounds.CalcWorldBounds();
    }

    /// <summary>Подставляет claimNameInput и memberNameInput в поля TextInput.</summary>
    private void ApplyClaimsPageInputState()
    {
        if (activePage == PageMap || SingleComposer == null)
        {
            return;
        }

        SingleComposer.GetTextInput("claimNameInput")?.SetValue(claimNameInput, true);
        SingleComposer.GetTextInput("memberNameInput")?.SetValue(memberNameInput, true);
    }

    #endregion

    #region Карта — центрирование и подписи

    /// <summary>Кнопка «Центр»: позиция игрока и запрос обновления карты.</summary>
    private bool CenterButton()
    {
        CenterOnPlayer();
        gridElement?.CenterMapOnPlayer();
        RequestRefresh();
        return true;
    }

    /// <summary>Вычисляет centerChunkX/Z по блоковой позиции игрока.</summary>
    private void CenterOnPlayer()
    {
        var player = clientApi.World.Player?.Entity;
        if (player == null)
        {
            centerChunkX = 0;
            centerChunkZ = 0;
            return;
        }

        var blockPos = player.Pos.AsBlockPos;
        var chunkSize = GlobalConstants.ChunkSize;
        centerChunkX = FloorDiv(blockPos.X, chunkSize);
        centerChunkZ = FloorDiv(blockPos.Z, chunkSize);
    }

    /// <summary>Обновляет подсказку по управлению картой (с учётом прав админа).</summary>
    private void UpdateHint(ClaimMapStatePacket? packet)
    {
        packet ??= mapState;
        if (packet == null || activePage != PageMap)
        {
            return;
        }

        var hintKey = packet.CanAdminUnclaimOthers
            ? "swixyclaimchunk:claim-map-hint-admin"
            : "swixyclaimchunk:claim-map-hint";
        SingleComposer?.GetDynamicText("hintLabel")?.SetNewText(Lang.Get(hintKey));
    }

    /// <summary>Обновляет подписи квот (чанки/области) и сообщение на вкладке карты.</summary>
    private void UpdateText(ClaimMapStatePacket? packet)
    {
        packet ??= mapState;
        if (packet == null || activePage != PageMap)
        {
            return;
        }

        var chunkSize = packet.ChunkSize > 0 ? packet.ChunkSize : GlobalConstants.ChunkSize;
        var mapSizeY = packet.MapSizeY > 0 ? packet.MapSizeY : 256;
        var usedChunks = ClaimVolumeUtil.BlocksToChunkCount(packet.UsedVolume, chunkSize, mapSizeY);
        var maxChunks = packet.MaxVolume > 0
            ? ClaimVolumeUtil.BlocksToChunkCount(packet.MaxVolume, chunkSize, mapSizeY)
            : 0;
        var maxChunksText = maxChunks > 0 ? maxChunks.ToString() : Lang.Get("swixyclaimchunk:claim-map-unlimited");
        var maxAreasText = packet.MaxAreas > 0 ? packet.MaxAreas.ToString() : Lang.Get("swixyclaimchunk:claim-map-unlimited");
        SingleComposer?.GetDynamicText("usedText").SetNewText(
            Lang.Get("swixyclaimchunk:claim-map-used-lines", usedChunks, maxChunksText, packet.UsedAreas, maxAreasText));
        SingleComposer?.GetDynamicText("messageText").SetNewText(packet.Message ?? "");
    }

    #endregion

    #region Отрисовка (Cairo)

    /// <summary>Тёмный фон боковых панелей с рамкой.</summary>
    private static void DrawSidePanelBackground(Cairo.Context ctx, Cairo.ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGBA(0, 0, 0, 0.96);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ctx.SetSourceRGBA(0, 0, 0, 1);
        ctx.LineWidth = 4;
        ctx.Rectangle(2, 2, width - 4, height - 4);
        ctx.Stroke();
    }

    /// <summary>Фон полей ввода на вкладке приватов.</summary>
    private static void DrawTextInputBackground(Cairo.Context ctx, Cairo.ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGBA(0.06, 0.065, 0.07, 0.98);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();

        ctx.SetSourceRGBA(0.85, 0.9, 0.95, 0.7);
        ctx.LineWidth = 1.5;
        ctx.Rectangle(1, 1, width - 2, height - 2);
        ctx.Stroke();
    }

    /// <summary>Отдельный фон скролл-зоны списка участников, чтобы она отличалась от общей панели.</summary>
    private static void DrawScrollAreaBackground(Cairo.Context ctx, Cairo.ImageSurface surface, ElementBounds bounds)
    {
        var width = bounds.OuterWidth;
        var height = bounds.OuterHeight;

        ctx.SetSourceRGBA(0.035, 0.04, 0.045, 0.98);
        ctx.Rectangle(0, 0, width, height);
        ctx.Fill();
    }

    /// <summary>Легенда цветов чанков на вкладке карты.</summary>
    private void DrawLegend(Cairo.Context ctx, Cairo.ImageSurface surface, ElementBounds bounds)
    {
        DrawLegendRow(ctx, 0, 0.16, 0.19, 0.17, Lang.Get("swixyclaimchunk:claim-map-legend-free"));
        DrawLegendRow(ctx, 28, 0.08, 0.42, 0.46, Lang.Get("swixyclaimchunk:claim-map-legend-own"));
        DrawLegendRow(ctx, 56, 0.55, 0.18, 0.16, Lang.Get("swixyclaimchunk:claim-map-legend-other"));
    }

    /// <summary>Одна строка легенды: цветной квадрат и подпись.</summary>
    private static void DrawLegendRow(Cairo.Context ctx, double y, double r, double g, double b, string text)
    {
        ctx.SetSourceRGB(r, g, b);
        ctx.Rectangle(0, y + 1, 22, 22);
        ctx.Fill();

        var font = CairoFont.WhiteSmallText();
        font.SetupContext(ctx);
        ctx.MoveTo(32, y + 18);
        ctx.ShowText(text);
    }

    /// <summary>Закрытие по крестику в заголовке.</summary>
    private void OnTitleBarClose()
    {
        TryClose();
    }

    /// <summary>Целочисленное деление с округлением вниз (для отрицательных координат).</summary>
    private static int FloorDiv(int value, int divisor)
    {
        return (int)System.Math.Floor((double)value / divisor);
    }

    #endregion
}
