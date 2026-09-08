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
    files = consultar("measures/component_tree", {
        "component": "SchoolManager", "pullRequest": pr, "qualifiers": "FIL", "ps": 500,
        "metricKeys": "new_lines,new_coverage,new_uncovered_lines,new_conditions_to_cover,new_uncovered_conditions,new_duplicated_lines,new_duplicated_lines_density",
    })
    for file in files["components"]:
        if file.get("measures"):
            print(json.dumps({"file": file.get("path"), "measures": file["measures"]}))
        if any(m["metric"] == "new_duplicated_lines" and any(float(p["value"]) > 0 for p in m.get("periods", [])) for m in file.get("measures", [])):
            print(json.dumps(consultar("duplications/show", {"key": file["key"], "pullRequest": pr})))
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
