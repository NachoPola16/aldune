package app.aldune.data

import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import app.aldune.domain.KeyMissingException
import app.aldune.domain.KeyWrapper
import java.security.KeyStore
import java.security.ProviderException
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * Clave de envoltura en el Android Keystore: AES-256-GCM, no exportable. Solo envuelve la clave de datos (ver `DataKeyManager`); las
 * notas nunca se cifran directamente con ella, así cada lectura no cuesta una llamada al hardware.
 *
 * Decisiones (spec de F4 §2.1):
 * - **Sin autenticación de usuario** y **sin `setUnlockedDeviceRequired`**: la sync en segundo plano y el widget tienen que poder leer
 *   las notas con el móvil bloqueado. La protección frente a quien tenga el móvil es el bloqueo del sistema y las notas protegidas.
 * - **StrongBox si existe** (chip de seguridad aparte, p. ej. el Titan del Pixel); si no, el TEE normal. Es más lento, pero solo se usa
 *   una vez al abrir la app.
 * - El Keystore pone el nonce (aleatorio) y se guarda delante del texto cifrado: blob = nonce (12) + cifrado + tag (16).
 */
class AndroidKeystoreKeyWrapper(private val alias: String = DEFAULT_ALIAS) : KeyWrapper {
    private val keyStore: KeyStore = KeyStore.getInstance(PROVIDER).apply { load(null) }

    override fun hasKey(): Boolean = keyStore.containsAlias(alias)

    override fun ensureKey() {
        if (hasKey()) return
        val strongBox = Build.VERSION.SDK_INT >= Build.VERSION_CODES.P
        try {
            generate(strongBox)
        } catch (e: ProviderException) {
            // StrongBoxUnavailableException hereda de ProviderException; se captura la madre para no cargar una clase de API 28
            // en un Android 8.
            if (!strongBox) throw e
            generate(strongBox = false)
        }
    }

    /** Si la clave vive en hardware seguro. Solo informativo (diagnóstico); en un emulador es `false`. */
    fun isHardwareBacked(): Boolean {
        val key = secretKey() ?: return false
        val info = javax.crypto.SecretKeyFactory.getInstance(key.algorithm, PROVIDER)
            .getKeySpec(key, android.security.keystore.KeyInfo::class.java) as android.security.keystore.KeyInfo
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            info.securityLevel == KeyProperties.SECURITY_LEVEL_TRUSTED_ENVIRONMENT ||
                info.securityLevel == KeyProperties.SECURITY_LEVEL_STRONGBOX
        } else {
            @Suppress("DEPRECATION")
            info.isInsideSecureHardware
        }
    }

    override fun wrap(plain: ByteArray): ByteArray {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, secretKey() ?: throw KeyMissingException())
        return cipher.iv + cipher.doFinal(plain)
    }

    override fun unwrap(blob: ByteArray): ByteArray {
        val key = secretKey() ?: throw KeyMissingException()
        if (blob.size < NONCE_BYTES + TAG_BITS / 8) throw javax.crypto.AEADBadTagException("Envoltorio demasiado corto")
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(TAG_BITS, blob, 0, NONCE_BYTES))
        return cipher.doFinal(blob, NONCE_BYTES, blob.size - NONCE_BYTES)
    }

    override fun delete() {
        if (hasKey()) keyStore.deleteEntry(alias)
    }

    private fun secretKey(): SecretKey? = keyStore.getKey(alias, null) as SecretKey?

    private fun generate(strongBox: Boolean) {
        val spec = KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setKeySize(256)
            .setUserAuthenticationRequired(false)
            .apply { if (strongBox) setIsStrongBoxBacked(true) }
            .build()
        KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, PROVIDER).run {
            init(spec)
            generateKey()
        }
    }

    companion object {
        const val DEFAULT_ALIAS = "aldune.datakey.wrap.v1"
        private const val PROVIDER = "AndroidKeyStore"
        private const val TRANSFORMATION = "AES/GCM/NoPadding"
        private const val NONCE_BYTES = 12
        private const val TAG_BITS = 128
    }
}
