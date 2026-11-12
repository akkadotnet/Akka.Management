#!/usr/bin/env python3
"""Fail CI unless the Aspire test run contains both successful integration tests."""
import sys
import xml.etree.ElementTree as ET

REQUIRED = {
    "Akka.Aspire.Hosting.Tests.AspireClusterFormationSpecs.Service_should_form_a_three_node_cluster_and_report_healthy",
    "Akka.Aspire.Hosting.Tests.AspireClusterFormationSpecs.Service_should_respond_to_root_endpoint",
}


def verify(path):
    root = ET.parse(path).getroot()
    results = root.findall("./{*}Results/{*}UnitTestResult")
    if not results:
        raise ValueError("No test results found")
    for result in results:
        if result.get("outcome") != "Passed":
            raise ValueError(f"Test did not pass: {result.get('testName')} ({result.get('outcome')})")
    for name in REQUIRED:
        matches = [result for result in results if result.get("testName") == name]
        if len(matches) != 1:
            raise ValueError(f"Expected exactly one result for {name}; found {len(matches)}")


if __name__ == "__main__":
    try:
        verify(sys.argv[1])
    except (IndexError, OSError, ET.ParseError, ValueError) as error:
        sys.exit(f"Aspire results validation failed: {error}")
    print("Aspire integration tests both passed")
