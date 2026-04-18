#!/usr/bin/env python3
"""
Hook PostToolUse — Architecture Guard
Corre después de cada Write/Edit en archivos .cs
Detecta violaciones de arquitectura en tiempo real
"""

import sys
import json
import re
import os

def check_file(filepath: str) -> list[str]:
    """Verifica un archivo .cs y retorna lista de violaciones."""
    if not filepath or not filepath.endswith('.cs'):
        return []

    if not os.path.exists(filepath):
        return []

    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
            lines = content.splitlines()
    except Exception:
        return []

    violations = []
    rel_path = filepath.replace(os.getcwd(), '').lstrip('/\\')

    # Determinar si es archivo de dominio (no en Presentation/ ni ScriptableObjects/)
    is_domain = (
        '/Scripts/' in filepath and
        '/Presentation/' not in filepath and
        '/ScriptableObjects/' not in filepath
    )

    # Determinar si es ScriptableObject
    is_so = '/ScriptableObjects/' in filepath

    for i, line in enumerate(lines, 1):
        stripped = line.strip()

        if is_domain and not is_so:
            # Dominio no debe heredar de MonoBehaviour
            if re.search(r'class\s+\w+\s*:\s*MonoBehaviour', stripped):
                violations.append(
                    f"🔴 CRÍTICO [{rel_path}:{i}] Clase de dominio hereda MonoBehaviour. "
                    f"Mueve a Presentation/ o elimina la herencia."
                )

            # Dominio no debe usar FindObjectOfType / GameObject.Find
            if 'FindObjectOfType' in stripped or 'GameObject.Find' in stripped:
                violations.append(
                    f"🔴 CRÍTICO [{rel_path}:{i}] Uso de {('FindObjectOfType' if 'FindObjectOfType' in stripped else 'GameObject.Find')} "
                    f"en dominio. Usa ServiceLocator o inyección de dependencias."
                )

            # Dominio no debe tener campos de tipo GameObject/Transform/Animator
            # como campos de instancia (no como parámetros de método)
            if re.search(r'(private|public|protected)\s+(GameObject|Transform|Animator|Rigidbody|Canvas)\s+\w+', stripped):
                violations.append(
                    f"🟡 ADVERTENCIA [{rel_path}:{i}] Campo Unity ({stripped.split()[1]}) "
                    f"en clase de dominio. Los MonoBehaviours van en Presentation/."
                )

        # En cualquier archivo: GetComponent en Update es peligroso
        if 'void Update' in stripped:
            # Buscar GetComponent en las siguientes 20 líneas
            window = lines[i:min(i+20, len(lines))]
            for j, wline in enumerate(window):
                if 'GetComponent' in wline and '//' not in wline.split('GetComponent')[0]:
                    violations.append(
                        f"🟡 ADVERTENCIA [{rel_path}:{i+j+1}] GetComponent() dentro de Update(). "
                        f"Cachea la referencia en Awake() o Start()."
                    )
                    break

        # ScriptableObjects deben tener CreateAssetMenu
        if is_so and 'public class' in stripped and 'ScriptableObject' in stripped:
            # Verificar que las líneas anteriores tengan [CreateAssetMenu]
            prev_lines = lines[max(0, i-5):i]
            has_create_menu = any('[CreateAssetMenu' in pl for pl in prev_lines)
            if not has_create_menu:
                violations.append(
                    f"🟡 ADVERTENCIA [{rel_path}:{i}] ScriptableObject sin [CreateAssetMenu]. "
                    f"Añade el atributo para poder crearlo desde el inspector."
                )

    return violations


def main():
    """Entry point del hook. Lee input de Claude Code vía stdin."""
    try:
        raw = sys.stdin.read()
        if not raw.strip():
            sys.exit(0)

        data = json.loads(raw)
        tool_name = data.get('tool_name', '')
        tool_input = data.get('tool_input', {})

        # Obtener el filepath del input de la herramienta
        filepath = tool_input.get('file_path') or tool_input.get('path', '')

        if not filepath:
            sys.exit(0)

        violations = check_file(filepath)

        if violations:
            # Retornar output que Claude Code mostrará como contexto
            output = {
                "type": "text",
                "text": (
                    "\n⚠️  ARCHITECTURE GUARD — Violaciones detectadas:\n" +
                    "\n".join(f"  {v}" for v in violations) +
                    "\n\nCorrige estas violaciones antes de continuar con la siguiente tarea.\n"
                )
            }
            print(json.dumps(output))

    except json.JSONDecodeError:
        sys.exit(0)
    except Exception as e:
        # No interrumpir el flujo de Claude Code por errores del hook
        sys.exit(0)


if __name__ == '__main__':
    main()
