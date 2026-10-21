"""Inspect an FBX without changing it; print geometry/rig/texture metadata as JSON.

Usage: python inspect_fbx.py path/to/model.fbx
Uses only the Python standard library. Bounds are raw geometry-local coordinates,
before FBX model transforms or unit/axis conversion. Triangles use n-gon fan counts.
"""

import json
import struct
import sys
import zlib


BINARY_MAGIC = b"Kaydara FBX Binary  \x00\x1a\x00"


def clean_name(value):
    if not isinstance(value, str):
        return str(value)
    return value.split("\x00", 1)[0]


def binary_nodes(data):
    version = struct.unpack_from("<I", data, 23)[0]
    wide = version >= 7500
    header_format = "<QQQB" if wide else "<IIIB"
    header_size = struct.calcsize(header_format)
    position = 27

    def read_property():
        nonlocal position
        kind = chr(data[position])
        position += 1
        scalars = {"Y": "h", "C": "?", "I": "i", "F": "f", "D": "d", "L": "q"}
        if kind in scalars:
            fmt = "<" + scalars[kind]
            result = struct.unpack_from(fmt, data, position)[0]
            position += struct.calcsize(fmt)
            return result
        if kind in ("S", "R"):
            length = struct.unpack_from("<I", data, position)[0]
            position += 4
            start = position
            position += length
            if position > len(data):
                raise ValueError("FBX property extends beyond the file")
            if kind == "R":
                return {"raw_byte_count": length}
            return data[start:position].decode("utf-8", errors="replace")
        arrays = {"f": "f", "d": "d", "l": "q", "i": "i", "b": "B", "c": "b"}
        if kind in arrays:
            length, encoding, packed_length = struct.unpack_from("<III", data, position)
            position += 12
            payload = data[position:position + packed_length]
            position += packed_length
            if encoding == 1:
                payload = zlib.decompress(payload)
            elif encoding != 0:
                raise ValueError("Unsupported FBX array encoding: " + str(encoding))
            fmt = "<" + str(length) + arrays[kind]
            expected = struct.calcsize(fmt)
            if len(payload) != expected:
                raise ValueError("FBX array byte count mismatch")
            return list(struct.unpack(fmt, payload))
        raise ValueError("Unsupported FBX property type: " + repr(kind))

    def read_node():
        nonlocal position
        if position + header_size > len(data):
            raise ValueError("Truncated FBX node header")
        end, property_count, property_bytes, name_length = struct.unpack_from(
            header_format, data, position
        )
        position += header_size
        if end == 0:
            return None
        if end > len(data) or end < position + name_length:
            raise ValueError("Invalid FBX node end offset")
        name = data[position:position + name_length].decode("utf-8", errors="replace")
        position += name_length
        properties_end = position + property_bytes
        properties = [read_property() for _ in range(property_count)]
        if position != properties_end:
            raise ValueError("FBX property-list byte count mismatch in " + name)
        children = []
        while position + header_size <= end:
            child = read_node()
            if child is None:
                break
            children.append(child)
        if position > end:
            raise ValueError("FBX node exceeds its declared end in " + name)
        position = end
        return {"name": name, "properties": properties, "children": children}

    nodes = []
    while position + header_size <= len(data):
        node = read_node()
        if node is None:
            break
        nodes.append(node)
    return nodes, version


def ascii_nodes(text):
    """Parse ordinary ASCII FBX nodes, including multiline a: numeric arrays."""
    tokens = []
    position = 0
    while position < len(text):
        char = text[position]
        if char == ";":
            while position < len(text) and text[position] not in "\r\n":
                position += 1
        elif char in " \t\r":
            position += 1
        elif char == "\n" or char in ":,{}*":
            tokens.append((char, char))
            position += 1
        elif char == '"':
            position += 1
            value = []
            while position < len(text):
                char = text[position]
                position += 1
                if char == '"':
                    break
                if char == "\\" and position < len(text) and text[position] in ('"', "\\"):
                    char = text[position]
                    position += 1
                value.append(char)
            tokens.append(("value", "".join(value)))
        else:
            start = position
            while position < len(text) and text[position] not in " \t\r\n:,{}*;":
                position += 1
            value = text[start:position]
            try:
                value = float(value) if any(c in value for c in ".eE") else int(value)
            except ValueError:
                pass
            tokens.append(("value", value))

    position = 0

    def skip_lines():
        nonlocal position
        while position < len(tokens) and tokens[position][0] == "\n":
            position += 1

    def read_node():
        nonlocal position
        skip_lines()
        if position >= len(tokens) or tokens[position][0] == "}":
            return None
        name = str(tokens[position][1])
        position += 1
        if position >= len(tokens) or tokens[position][0] != ":":
            raise ValueError("Expected ':' after ASCII FBX node " + name)
        position += 1
        properties = []
        while position < len(tokens):
            kind, value = tokens[position]
            if kind in ("{", "}") or (kind == "\n" and name != "a"):
                break
            position += 1
            if kind == "value":
                properties.append(value)
        skip_lines()
        children = []
        if position < len(tokens) and tokens[position][0] == "{":
            position += 1
            while True:
                skip_lines()
                if position >= len(tokens):
                    raise ValueError("Unclosed ASCII FBX node " + name)
                if tokens[position][0] == "}":
                    position += 1
                    break
                child = read_node()
                if child is not None:
                    children.append(child)
        return {"name": name, "properties": properties, "children": children}

    nodes = []
    while position < len(tokens):
        node = read_node()
        if node is None:
            break
        nodes.append(node)
    header = next((n for n in nodes if n["name"] == "FBXHeaderExtension"), None)
    version_node = child_named(header, "FBXVersion") if header else None
    version = version_node["properties"][0] if version_node else None
    return nodes, version


def child_named(node, name):
    return next((c for c in node["children"] if c["name"] == name), None)


def child_value(node, name, default=None):
    child = child_named(node, name)
    return child["properties"][0] if child and child["properties"] else default


def array_values(node, name):
    child = child_named(node, name)
    if child is None:
        return []
    if child["properties"] and isinstance(child["properties"][0], list):
        return child["properties"][0]
    values = child_named(child, "a")
    return values["properties"] if values else []


def axis_bounds(vertices):
    if not vertices:
        return None
    result = {}
    for offset, axis in enumerate(("x", "y", "z")):
        values = vertices[offset::3]
        if not values:
            return None
        low, high = min(values), max(values)
        result[axis] = {"min": low, "max": high, "size": high - low}
    return result


def inspect(path):
    with open(path, "rb") as source:
        data = source.read()
    binary = data.startswith(BINARY_MAGIC)
    nodes, version = binary_nodes(data) if binary else ascii_nodes(data.decode("utf-8-sig"))
    roots = {n["name"]: n for n in nodes}
    objects = roots.get("Objects", {"children": []})["children"]
    connections = roots.get("Connections", {"children": []})["children"]
    parents = {}
    for connection in connections:
        props = connection["properties"]
        if connection["name"] == "C" and len(props) >= 3 and props[0] == "OO":
            parents[props[1]] = props[2]

    geometry = []
    materials = []
    textures = []
    bones = []
    attributes = []
    skins = []
    clusters = []
    warnings = []
    for node in objects:
        props = node["properties"]
        identifier = props[0] if props else None
        name = clean_name(props[1]) if len(props) > 1 else ""
        subtype = props[2] if len(props) > 2 else ""
        if node["name"] == "Geometry":
            vertices = array_values(node, "Vertices")
            indices = array_values(node, "PolygonVertexIndex")
            face_count = 0
            triangles = 0
            polygon_size = 0
            largest_polygon = 0
            invalid_indices = 0
            vertex_count = len(vertices) // 3
            for index in indices:
                polygon_size += 1
                decoded_index = -index - 1 if index < 0 else index
                if decoded_index < 0 or decoded_index >= vertex_count:
                    invalid_indices += 1
                if index < 0:
                    face_count += 1
                    triangles += max(0, polygon_size - 2)
                    largest_polygon = max(largest_polygon, polygon_size)
                    polygon_size = 0
            if len(vertices) % 3:
                warnings.append("Incomplete XYZ vertex in geometry " + name)
            if polygon_size:
                warnings.append("Unterminated polygon in geometry " + name)
            if invalid_indices:
                warnings.append("Out-of-range polygon indices in geometry " + name)
            geometry.append({
                "id": identifier, "name": name, "type": subtype,
                "vertices": vertex_count, "faces": face_count,
                "fan_triangles": triangles, "largest_polygon_vertices": largest_polygon,
                "invalid_vertex_indices": invalid_indices,
                "local_axis_bounds": axis_bounds(vertices),
            })
        elif node["name"] == "Material":
            materials.append({"id": identifier, "name": name})
        elif node["name"] in ("Texture", "Video"):
            content = child_named(node, "Content")
            raw = content["properties"][0] if content and content["properties"] else None
            embedded_bytes = raw.get("raw_byte_count", 0) if isinstance(raw, dict) else 0
            # ASCII FBX may store embedded content as a base64 string.
            embedded = bool(embedded_bytes or (isinstance(raw, str) and raw))
            textures.append({
                "id": identifier, "name": name, "object_type": node["name"],
                "filename": child_value(node, "FileName", child_value(node, "Filename")),
                "relative_filename": child_value(node, "RelativeFilename"),
                "embedded": embedded, "embedded_byte_count": embedded_bytes or None,
            })
        elif node["name"] == "Model" and subtype in ("LimbNode", "Root", "Skeleton"):
            bones.append({"id": identifier, "name": name, "type": subtype,
                          "parent_id": parents.get(identifier)})
        elif node["name"] == "NodeAttribute" and subtype in ("LimbNode", "Root", "Skeleton"):
            attributes.append({"id": identifier, "name": name, "type": subtype})
        elif node["name"] == "Deformer" and subtype == "Skin":
            skins.append({"id": identifier, "name": name})
        elif node["name"] == "Deformer" and subtype == "Cluster":
            clusters.append({"id": identifier, "name": name,
                             "weighted_vertex_entries": len(array_values(node, "Indexes"))})

    settings = {}
    global_settings = roots.get("GlobalSettings")
    property_settings = child_named(global_settings, "Properties70") if global_settings else None
    if property_settings:
        for setting in property_settings["children"]:
            props = setting["properties"]
            if setting["name"] == "P" and len(props) >= 5:
                if props[0] in ("UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign",
                                "CoordAxis", "CoordAxisSign", "UnitScaleFactor"):
                    settings[props[0]] = props[-1]

    bone_ids = {b["id"] for b in bones}
    return {
        "file": path, "format": "binary" if binary else "ascii", "fbx_version": version,
        "file_bytes": len(data), "global_axis_and_unit_settings": settings,
        "bounds_space": "raw geometry local; model transforms and unit conversion not applied",
        "totals": {"geometries": len(geometry),
                   "vertices": sum(g["vertices"] for g in geometry),
                   "faces": sum(g["faces"] for g in geometry),
                   "fan_triangles": sum(g["fan_triangles"] for g in geometry),
                   "materials": len(materials)},
        "geometry": geometry, "materials": materials, "textures": textures,
        "embedded_texture_filenames": [t["filename"] or t["relative_filename"] or t["name"]
                                       for t in textures if t["embedded"]],
        "skeleton": {"bone_count": len(bones), "bones": bones,
                     "root_bone_names": [b["name"] for b in bones if b["parent_id"] not in bone_ids],
                     "skeleton_node_attributes": attributes,
                     "skin_deformer_count": len(skins), "skin_deformers": skins,
                     "cluster_count": len(clusters), "clusters": clusters},
        "warnings": warnings,
    }


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python inspect_fbx.py path/to/model.fbx", file=sys.stderr)
        sys.exit(2)
    try:
        print(json.dumps(inspect(sys.argv[1]), ensure_ascii=False, indent=2))
    except (OSError, ValueError, IndexError, KeyError, struct.error, zlib.error) as error:
        print(json.dumps({"file": sys.argv[1], "error": str(error)}, ensure_ascii=False))
        sys.exit(1)
