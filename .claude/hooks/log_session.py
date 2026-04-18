#!/usr/bin/env python3
"""
Hook Stop — Session Logger
Corre automáticamente cuando Claude Code termina una sesión.
Registra la fecha/hora en sprint-active.md para tener trazabilidad.
"""

import sys
import json
import os
from datetime import datetime


def main():
    sprint_file = '.claude/sprint-active.md'

    if not os.path.exists(sprint_file):
        sys.exit(0)

    try:
        now = datetime.now().strftime('%Y-%m-%d %H:%M')

        with open(sprint_file, 'r', encoding='utf-8') as f:
            content = f.read()

        # Añadir registro de sesión cerrada si no existe ya para esta hora
        log_entry = f"\n<!-- sesión cerrada: {now} -->"

        if log_entry not in content:
            with open(sprint_file, 'a', encoding='utf-8') as f:
                f.write(log_entry)

    except Exception:
        pass

    sys.exit(0)


if __name__ == '__main__':
    main()
