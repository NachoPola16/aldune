package app.aldune.ui

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.view.WindowManager
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContract
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.LocalLifecycleOwner
import app.aldune.R
import app.aldune.core.ProfileCodes
import app.aldune.data.SettingsStore
import app.aldune.syncclient.QrDecoder
import java.util.concurrent.Executors
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/** Pide el escáner y devuelve el código de perfil leído, o `null` si se cancela o no hay permiso. */
class ScanQrContract : ActivityResultContract<Unit, String?>() {
    override fun createIntent(context: Context, input: Unit): Intent = Intent(context, QrScanActivity::class.java)

    override fun parseResult(resultCode: Int, intent: Intent?): String? =
        if (resultCode == Activity.RESULT_OK) intent?.getStringExtra(QrScanActivity.EXTRA_CODE) else null
}

/**
 * La pantalla del escáner de «Añadir perfil». El QR lleva la clave del perfil, así que: `FLAG_SECURE` (ni capturas ni miniatura en Recientes),
 * ningún fotograma se guarda ni sale de aquí, y la cámara solo está abierta mientras esta pantalla está delante.
 */
class QrScanActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        val settings = SettingsStore(this).load()
        setContent {
            AldunaTheme(settings.appearance, settings.aspectColors) {
                Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
                    ScanScreen(onCode = { code ->
                        setResult(RESULT_OK, Intent().putExtra(EXTRA_CODE, code))
                        finish()
                    }, onBack = { finish() })
                }
            }
        }
    }

    companion object {
        const val EXTRA_CODE = "profile_code"
    }
}

@Composable
private fun ScanScreen(onCode: (String) -> Unit, onBack: () -> Unit) {
    val context = LocalContext.current
    var granted by remember { mutableStateOf(ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) }
    var asked by remember { mutableStateOf(false) }
    val permission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { ok -> granted = ok; asked = true }
    LaunchedEffect(Unit) { if (!granted) permission.launch(Manifest.permission.CAMERA) }
    when {
        granted -> CameraPreview(onCode, onBack)
        asked -> Column(Modifier.fillMaxSize().padding(24.dp), verticalArrangement = Arrangement.Center, horizontalAlignment = Alignment.CenterHorizontally) {
            Text(stringResource(R.string.scan_qr_denied), style = MaterialTheme.typography.bodyLarge)
            Button(onClick = onBack, Modifier.padding(top = 16.dp)) { Text(stringResource(R.string.scan_qr_back)) }
        }
        else -> Box(Modifier.fillMaxSize())
    }
}

@Composable
private fun CameraPreview(onCode: (String) -> Unit, onBack: () -> Unit) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val notAldune = stringResource(R.string.scan_qr_not_aldune)
    val done = remember { AtomicBoolean(false) }
    val lastWarning = remember { AtomicLong(0) }
    val executor = remember { Executors.newSingleThreadExecutor() }
    val main = remember { ContextCompat.getMainExecutor(context) }
    var provider by remember { mutableStateOf<ProcessCameraProvider?>(null) }
    // La cámara solo está abierta mientras esta pantalla está delante: al salir se desenlaza y se para el hilo de análisis.
    DisposableEffect(Unit) {
        onDispose {
            provider?.unbindAll()
            executor.shutdown()
        }
    }
    Box(Modifier.fillMaxSize()) {
        AndroidView(
            factory = { ctx ->
                val view = PreviewView(ctx)
                val future = ProcessCameraProvider.getInstance(ctx)
                future.addListener({
                    val p = future.get()
                    provider = p
                    val analysis = ImageAnalysis.Builder().setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST).build()
                    analysis.setAnalyzer(executor) { image ->
                        analyze(
                            image, done,
                            // El análisis va en su hilo: el resultado y el aviso se pasan al hilo principal.
                            onCode = { code -> main.execute { onCode(code) } },
                            onOther = {
                                // Un QR que no es de Aldune se ignora y se sigue buscando; el aviso se limita a uno cada 3 s.
                                val now = System.currentTimeMillis()
                                if (now - lastWarning.get() > 3_000) {
                                    lastWarning.set(now)
                                    main.execute { Toast.makeText(ctx, notAldune, Toast.LENGTH_SHORT).show() }
                                }
                            },
                        )
                    }
                    val preview = Preview.Builder().build().also { it.surfaceProvider = view.surfaceProvider }
                    p.unbindAll()
                    p.bindToLifecycle(lifecycleOwner, CameraSelector.DEFAULT_BACK_CAMERA, preview, analysis)
                }, main)
                view
            },
            modifier = Modifier.fillMaxSize(),
        )
        Text(
            stringResource(R.string.scan_qr_title), Modifier.align(Alignment.TopCenter).padding(24.dp),
            color = androidx.compose.ui.graphics.Color.White, style = MaterialTheme.typography.titleMedium,
        )
        Button(onClick = onBack, Modifier.align(Alignment.BottomCenter).padding(24.dp)) { Text(stringResource(R.string.scan_qr_back)) }
    }
}

private fun analyze(image: ImageProxy, done: AtomicBoolean, onCode: (String) -> Unit, onOther: (String) -> Unit) {
    try {
        if (done.get()) return
        val plane = image.planes[0]
        val buffer = plane.buffer
        val bytes = ByteArray(buffer.remaining()).also { buffer.get(it) }
        val text = QrDecoder.decodeText(bytes, plane.rowStride, image.width, image.height) ?: return
        val code = ProfileCodes.findShareCode(text)
        // `done` evita devolver dos veces si llegan dos fotogramas con el código antes de que se cierre la pantalla.
        if (code == null) onOther(text) else if (done.compareAndSet(false, true)) onCode(code)
    } finally {
        image.close()
    }
}
