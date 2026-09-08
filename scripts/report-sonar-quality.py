"""Publica métricas y hallazgos del PR sin registrar credenciales de Sonar."""

import base64
import json
import os
from urllib.parse import urlencode
from urllib.request import Request, urlopen


def consultar(path: str, params: dict) -> dict:
    url = "https://sonarcloud.io/api/" + path + "?" + urlencode(params)
    credential = base64.b64encode((os.environ["SONAR_TOKEN"] + ":").encode()).decode()
    request = Request(url, headers={"Authorization": "Basic " + credential})
    with urlopen(request, timeout=30) as response:
        return json.load(response)


def main() -> None:
    pr = os.environ["SONAR_PR"]
    gate = consultar("qualitygates/project_status", {"projectKey": "SchoolManager", "pullRequest": pr})
    print(json.dumps(gate["projectStatus"], ensure_ascii=False))
    page = 1
    while True:
        issues = consultar("issues/search", {
            "componentKeys": "SchoolManager", "pullRequest": pr,
            "resolved": "false", "ps": 100, "p": page,
        })
        for issue in issues["issues"]:
            print(json.dumps({key: issue.get(key) for key in (
                "rule", "component", "line", "message", "severity", "type"
            )}, ensure_ascii=False))
        if page * 100 >= issues["paging"]["total"]:
            break
        page += 1


if __name__ == "__main__":
    main()
