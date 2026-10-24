using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;

namespace Frontend_Mobile;

/// <summary>
/// Cards and rows that open on tap use a TapGestureRecognizer, which TalkBack reads as plain text.
/// This gives every such view the Button role, so TalkBack says "button" and "double-tap to activate".
/// It wraps any delegate MAUI already set (SemanticProperties.Hint uses one) instead of replacing it.
/// Reference: Android Developers, "Make apps more accessible." Accessed: Oct. 23, 2026. [Online]. Available: https://developer.android.com/guide/topics/ui/accessibility/apps
/// </summary>
internal sealed class TapAsButton(AccessibilityDelegateCompat? inner) : AccessibilityDelegateCompat
{
    public static void Register() =>
        Microsoft.Maui.Handlers.ViewHandler.ViewMapper.AppendToMapping(nameof(TapAsButton), (handler, view) =>
        {
            if (view is not View v || !v.GestureRecognizers.OfType<TapGestureRecognizer>().Any()) return;
            if (handler.PlatformView is not Android.Views.View native) return;
            var existing = ViewCompat.GetAccessibilityDelegate(native);
            if (existing is TapAsButton) return;
            ViewCompat.SetAccessibilityDelegate(native, new TapAsButton(existing));
        });

    //----------------------------------------------------------------------------------------------------------------------//
    public override void OnInitializeAccessibilityNodeInfo(Android.Views.View? host, AccessibilityNodeInfoCompat? info)
    {
        if (inner is not null) inner.OnInitializeAccessibilityNodeInfo(host, info);
        else base.OnInitializeAccessibilityNodeInfo(host, info);
        if (info is null) return;
        info.ClassName = "android.widget.Button";
        info.Clickable = true;
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//
