package app.aldune.widget

import android.content.Context

/**
 * The tag each dock widget is limited to, by widget id (`null` = all notes). It is a per-widget choice made in
 * [WidgetConfigActivity], so it lives here and not in the app settings; nothing about it syncs. The tag name is not secret
 * (the widget already shows titles) and a tag that no longer exists just gives an empty widget.
 */
object WidgetTagStore {
    private const val FILE = "widget_tags"

    private fun prefs(context: Context) = context.applicationContext.getSharedPreferences(FILE, Context.MODE_PRIVATE)

    fun get(context: Context, appWidgetId: Int): String? = prefs(context).getString(key(appWidgetId), null)?.takeIf { it.isNotBlank() }

    fun set(context: Context, appWidgetId: Int, tag: String?) {
        prefs(context).edit().apply { if (tag.isNullOrBlank()) remove(key(appWidgetId)) else putString(key(appWidgetId), tag) }.apply()
    }

    fun forget(context: Context, appWidgetIds: IntArray) {
        prefs(context).edit().apply { appWidgetIds.forEach { remove(key(it)) } }.apply()
    }

    private fun key(appWidgetId: Int) = "tag_$appWidgetId"
}
