// =============================================================================
// ClaimMapDialog.DeleteConfirm.cs
// -----------------------------------------------------------------------------
// Overlay confirm: modal.png + Group 1623/1625. Text from div.svg (no dimmer).
// Frame 781×321, buttons 314×67 at (71,184)/(396,184), copy 303×68 at y=70.
// =============================================================================

using System;
using System.Linq;
using Cairo;
using SwixyClaimChunk.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SwixyClaimChunk.Content;

public sealed partial class ClaimMapDialog
{
    private const string DeleteConfirmComposerName = "epclaimmap-delete";

    /// <summary>Full Group 1012-style modal frame (textures/gui/modal_delete.png).</summary>
    private const int DeleteModalW = 781;

    private const int DeleteModalH = 321;

    /// <summary>Group 1623 / 1625 button size.</summary>
    private const int DeleteBtnW = 314;

    private const int DeleteBtnH = 67;

    /// <summary>Buttons relative to modal.png (SVG 640/965,564 minus frame origin 569,380).</summary>
    private const int DeleteBtnY = 184;

    private const int DeleteCancelX = 71;

    private const int DeleteConfirmX = 396;

    /// <summary>div.svg text block 303×68, centered in the modal above the buttons.</summary>
    private const int DeleteTextW = 303;

    private const int DeleteTextH = 68;

    private const int DeleteTextY = 70;

    /// <summary>Question row in div.svg (3px Minecraft, y=0–21).</summary>
    private const int DeleteQuestionH = 21;

    /// <summary>Name row in div.svg (4px Minecraft, y=36–64).</summary>
    private const int DeleteNameY = 36;

    private const int DeleteNameH = 28;

    private const double FontDeleteQuestion = 21;

    private const double FontDeleteName = 28;

    private const double FontDeleteBtn = 22;

    /// <summary>div.svg question fill #555555.</summary>
    private static readonly double[] ColDeleteQuestion = [0x55 / 255.0, 0x55 / 255.0, 0x55 / 255.0, 1.0];

    /// <summary>div.svg name fill white.</summary>
    private static readonly double[] ColDeleteName = [1.0, 1.0, 1.0, 1.0];

    /// <summary>Cancel label #EBEBEB on Group 1623.</summary>
    private static readonly double[] ColDeleteCancelText = [0xEB / 255.0, 0xEB / 255.0, 0xEB / 255.0, 1.0];

    private int DeleteTextX => (DeleteModalW - DeleteTextW) / 2;

    private ImageSurface? deleteModalSurface;
    private ImageSurface? deleteCancelBtnSurface;
    private ImageSurface? deleteConfirmBtnSurface;

    private enum DeleteConfirmKind
    {
        None,
        Claim,
        Member
    }

    private bool deleteConfirmOpen;
    private DeleteConfirmKind deleteConfirmKind;
    private int deleteConfirmClaimId;
    private string deleteConfirmClaimName = "";
    private string deleteConfirmMemberUid = "";
    private string deleteConfirmMemberName = "";

    private int DeleteModalX => (UiW - DeleteModalW) / 2;

    private int DeleteModalY => (UiH - DeleteModalH) / 2;

    private void OpenDeleteConfirm(ClaimInfoPacket claim)
    {
        deleteConfirmKind = DeleteConfirmKind.Claim;
        deleteConfirmClaimId = claim.ClaimId;
        deleteConfirmClaimName = claim.Name ?? "";
        deleteConfirmMemberUid = "";
        deleteConfirmMemberName = "";
        deleteConfirmOpen = true;
        SelectClaim(claim);
        ComposeDeleteConfirmOverlay();
    }

    private void OpenDeleteMemberConfirm(ClaimMemberPacket member)
    {
        if (member.IsOwner || string.IsNullOrEmpty(member.PlayerUid))
        {
            return;
        }

        deleteConfirmKind = DeleteConfirmKind.Member;
        deleteConfirmClaimId = selectedClaimId;
        deleteConfirmClaimName = selectedClaimName ?? "";
        deleteConfirmMemberUid = member.PlayerUid;
        deleteConfirmMemberName = string.IsNullOrWhiteSpace(member.PlayerName)
            ? member.PlayerUid
            : member.PlayerName;
        deleteConfirmOpen = true;
        ComposeDeleteConfirmOverlay();
    }

    private void CloseDeleteConfirm()
    {
        deleteConfirmOpen = false;
        deleteConfirmKind = DeleteConfirmKind.None;
        deleteConfirmClaimId = 0;
        deleteConfirmClaimName = "";
        deleteConfirmMemberUid = "";
        deleteConfirmMemberName = "";
        DisposeDeleteConfirmComposer();
    }

    private void DisposeDeleteConfirmComposer()
    {
        if (!Composers.ContainsKey(DeleteConfirmComposerName))
        {
            return;
        }

        Composers[DeleteConfirmComposerName]?.Dispose();
        Composers.Remove(DeleteConfirmComposerName);
    }

    private void ComposeDeleteConfirmOverlay()
    {
        if (!deleteConfirmOpen || SingleComposer == null)
        {
            DisposeDeleteConfirmComposer();
            return;
        }

        EnsureFrameSurface();
        DisposeDeleteConfirmComposer();

        var overlayBounds = BuildDialogBounds();
        CopyComposerPosition(SingleComposer.Bounds, overlayBounds);

        // Draw-only overlay. Hits are handled in TryHandleDeleteConfirmMouseDown —
        // overlapping GuiElementTextButton (frame/dimmer) stole the delete click.
        var composer = clientApi.Gui
            .CreateCompo(DeleteConfirmComposerName, overlayBounds)
            .AddDynamicCustomDraw(
                ElementBounds.Fill,
                DrawDeleteConfirmOverlay,
                "deleteConfirmChrome");

        Composers[DeleteConfirmComposerName] = composer.Compose();
    }

    private void ConfirmDeleteFromModal()
    {
        if (deleteConfirmKind == DeleteConfirmKind.Member)
        {
            ConfirmDeleteMember();
            return;
        }

        ConfirmDeleteClaim();
    }

    private void ConfirmDeleteClaim()
    {
        var claimId = deleteConfirmClaimId;
        if (claimId <= 0)
        {
            CloseDeleteConfirm();
            return;
        }

        var claim = claimListState?.Claims.FirstOrDefault(c => c.ClaimId == claimId);
        CloseDeleteConfirm();

        if (claim != null)
        {
            SelectClaim(claim);
        }
        else
        {
            selectedClaimId = claimId;
        }

        if (highlightedClaimId == claimId)
        {
            channel.SendPacket(new ClaimShowRequestPacket
            {
                ClaimId = claimId,
                Clear = true
            });
            highlightedClaimId = 0;
            pendingHighlightClaimId = -1;
        }

        SendClaimAction(ClaimAccessActionType.DeleteClaim, "", 0, "");
    }

    private void ConfirmDeleteMember()
    {
        var uid = deleteConfirmMemberUid;
        var name = deleteConfirmMemberName;
        CloseDeleteConfirm();
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }

        SendClaimAction(ClaimAccessActionType.RemovePlayer, name, 0, "", uid);
    }

    private void SyncDeleteConfirmBounds()
    {
        if (!deleteConfirmOpen || SingleComposer?.Bounds == null)
        {
            return;
        }

        if (!Composers.ContainsKey(DeleteConfirmComposerName)
            || Composers[DeleteConfirmComposerName] == null)
        {
            ComposeDeleteConfirmOverlay();
            return;
        }

        CopyComposerPosition(SingleComposer.Bounds, Composers[DeleteConfirmComposerName]!.Bounds);
        Composers[DeleteConfirmComposerName]!.Bounds.CalcWorldBounds();
    }

    private static void CopyComposerPosition(ElementBounds from, ElementBounds to)
    {
        to.Alignment = from.Alignment;
        to.fixedX = from.fixedX;
        to.fixedY = from.fixedY;
        to.absMarginX = from.absMarginX;
        to.absMarginY = from.absMarginY;
    }

    private void DrawDeleteConfirmOverlay(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var w = bounds.OuterWidth;
        var h = bounds.OuterHeight;
        var s = Math.Max(0.01, RuntimeEnv.GUIScale);

        ctx.Operator = Operator.Source;
        ctx.SetSourceRGBA(0, 0, 0, 0);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        ctx.Operator = Operator.Over;

        var modalX = DeleteModalX * s;
        var modalY = DeleteModalY * s;
        var modalW = DeleteModalW * s;
        var modalH = DeleteModalH * s;

        if (deleteModalSurface != null && deleteModalSurface.Width > 0)
        {
            DrawGuiTexture(ctx, deleteModalSurface, modalX, modalY, modalW, modalH, fallback: ColPanel);
        }
        else
        {
            DrawFacePlateChrome(ctx, modalX, modalY, modalW, modalH);
        }

        DrawDeleteButtonTexture(
            ctx,
            deleteCancelBtnSurface,
            modalX + DeleteCancelX * s,
            modalY + DeleteBtnY * s,
            DeleteBtnW * s,
            DeleteBtnH * s,
            fallback: [0x3F / 255.0, 0x3F / 255.0, 0x3F / 255.0]);
        DrawDeleteButtonTexture(
            ctx,
            deleteConfirmBtnSurface,
            modalX + DeleteConfirmX * s,
            modalY + DeleteBtnY * s,
            DeleteBtnW * s,
            DeleteBtnH * s,
            fallback: [0xFD / 255.0, 0x5A / 255.0, 0x53 / 255.0]);

        // div.svg: "ВЫ ХОТИТЕ УДАЛИТЬ?" #555555, then "(NameReg)" white.
        var textX = modalX + DeleteTextX * s;
        var textY = modalY + DeleteTextY * s;
        var textW = DeleteTextW * s;
        DrawDeleteCenteredBody(
            ctx,
            Lang.Get("swixyclaimchunk:claims-delete-modal-title").ToUpperInvariant(),
            textX,
            textY,
            textW,
            DeleteQuestionH * s,
            FontDeleteQuestion,
            ColDeleteQuestion);

        var name = deleteConfirmKind == DeleteConfirmKind.Member
            ? deleteConfirmMemberName.Trim()
            : deleteConfirmClaimName.Trim();
        if (!string.IsNullOrEmpty(name))
        {
            DrawDeleteCenteredBody(
                ctx,
                Lang.Get("swixyclaimchunk:claims-delete-modal-name", name),
                textX,
                textY + DeleteNameY * s,
                textW,
                DeleteNameH * s,
                FontDeleteName,
                ColDeleteName);
        }

        DrawDeleteCenteredTitle(
            ctx,
            Lang.Get("swixyclaimchunk:claims-delete-modal-cancel").ToUpperInvariant(),
            modalX + DeleteCancelX * s,
            modalY + DeleteBtnY * s,
            DeleteBtnW * s,
            DeleteBtnH * s,
            FontDeleteBtn,
            ColDeleteCancelText);
        DrawDeleteCenteredTitle(
            ctx,
            Lang.Get("swixyclaimchunk:claims-delete-modal-confirm").ToUpperInvariant(),
            modalX + DeleteConfirmX * s,
            modalY + DeleteBtnY * s,
            DeleteBtnW * s,
            DeleteBtnH * s,
            FontDeleteBtn,
            ColTabActive);
    }

    private static void DrawDeleteButtonTexture(
        Context ctx,
        ImageSurface? tex,
        double x,
        double y,
        double w,
        double h,
        double[] fallback)
    {
        if (tex != null && tex.Width > 0 && tex.Height > 0)
        {
            DrawGuiTexture(ctx, tex, x, y, w, h, fallback);
            return;
        }

        ctx.SetSourceRGB(fallback[0], fallback[1], fallback[2]);
        ctx.Rectangle(x, y, w, h);
        ctx.Fill();
    }

    private static void DrawDeleteCenteredBody(
        Context ctx,
        string text,
        double x,
        double y,
        double width,
        double height,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupBody(ctx, designFontSize, color);
        DrawDeleteCenteredCurrentFont(ctx, text, x, y, width, height);
    }

    private static void DrawDeleteCenteredTitle(
        Context ctx,
        string text,
        double x,
        double y,
        double width,
        double height,
        double designFontSize,
        double[] color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ClaimFontHelper.SetupTitle(ctx, designFontSize, color);
        DrawDeleteCenteredCurrentFont(ctx, text, x, y, width, height);
    }

    private static void DrawDeleteCenteredCurrentFont(
        Context ctx,
        string text,
        double x,
        double y,
        double width,
        double height)
    {
        var draw = text;
        var extents = ctx.TextExtents(draw);
        if (extents.Width > width && width > 8)
        {
            while (draw.Length > 1 && ctx.TextExtents(draw + "…").Width > width)
            {
                draw = draw[..^1];
            }

            draw += "…";
            extents = ctx.TextExtents(draw);
        }

        var fe = ctx.FontExtents;
        var tx = x + (width - extents.Width) * 0.5 - extents.XBearing;
        var ty = y + (height - fe.Height) * 0.5 + fe.Ascent;
        ctx.MoveTo(tx, ty);
        ctx.ShowText(draw);
    }

    private bool TryHandleDeleteConfirmMouseDown(MouseEvent args)
    {
        if (!deleteConfirmOpen)
        {
            return false;
        }

        args.Handled = true;
        if (args.Button != EnumMouseButton.Left)
        {
            return true;
        }

        var mx = args.X;
        var my = args.Y;
        var modalX = DeleteModalX;
        var modalY = DeleteModalY;

        if (IsMouseOverRect(mx, my, modalX + DeleteConfirmX, modalY + DeleteBtnY, DeleteBtnW, DeleteBtnH))
        {
            clientApi.Gui.PlaySound("toggleswitch");
            ConfirmDeleteFromModal();
            return true;
        }

        if (IsMouseOverRect(mx, my, modalX + DeleteCancelX, modalY + DeleteBtnY, DeleteBtnW, DeleteBtnH)
            || !IsMouseOverRect(mx, my, modalX, modalY, DeleteModalW, DeleteModalH))
        {
            clientApi.Gui.PlaySound("toggleswitch");
            CloseDeleteConfirm();
            return true;
        }

        return true;
    }

    private bool IsOverDeleteConfirmButton(int mouseX, int mouseY)
    {
        if (!deleteConfirmOpen)
        {
            return false;
        }

        var modalX = DeleteModalX;
        var modalY = DeleteModalY;
        return IsMouseOverRect(mouseX, mouseY, modalX + DeleteCancelX, modalY + DeleteBtnY, DeleteBtnW, DeleteBtnH)
            || IsMouseOverRect(mouseX, mouseY, modalX + DeleteConfirmX, modalY + DeleteBtnY, DeleteBtnW, DeleteBtnH);
    }
}
