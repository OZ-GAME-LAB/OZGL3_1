"""Local preview media live outside the Unity project and Git checkout.

Paths under ArtSources are mirrored below Desktop/OZGL3_ArtReview by default.
Set OZGL3_ART_REVIEW_DIR to an absolute external directory to use another drive.
This module never moves or deletes existing files.
"""
from pathlib import Path
import html
import os
import re
from urllib.parse import unquote, urlsplit

ART_SOURCES = Path(__file__).resolve().parent
PROJECT = ART_SOURCES.parent


def review_root():
    configured = os.environ.get('OZGL3_ART_REVIEW_DIR')
    root = Path(configured).expanduser() if configured else Path.home() / 'Desktop' / 'OZGL3_ArtReview'
    if not root.is_absolute():
        raise ValueError('OZGL3_ART_REVIEW_DIR must be an absolute directory outside the project')
    root = root.resolve()
    if root == PROJECT or PROJECT in root.parents:
        raise ValueError('Review media must be stored outside the Unity project')
    return root


def media_path(art_source_path):
    """Map a source-relative review path to the external mirror."""
    source = Path(art_source_path).resolve()
    relative = source.relative_to(ART_SOURCES)
    return review_root() / relative


def storage_metadata(base=None):
    result = {
        'kind': 'external-local-review',
        'environmentVariable': 'OZGL3_ART_REVIEW_DIR',
        'defaultDirectory': '~/Desktop/OZGL3_ArtReview',
        'mirrorRelativeTo': 'ArtSources',
        'trackedMedia': False,
    }
    if base is not None:
        result['relativeBase'] = Path(base).resolve().relative_to(ART_SOURCES).as_posix()
    return result


def externalize_html_links(document, project_html):
    """Use external GIFs; keep source assets and still-image evidence in place."""
    parent = Path(project_html).resolve().parent
    def replace(match):
        attr, quote, value = match.groups()
        parsed = urlsplit(html.unescape(value))
        if parsed.scheme or value.startswith('#'):
            return match.group(0)
        target = (parent / unquote(parsed.path)).resolve()
        if target.suffix.lower() == '.gif':
            target = media_path(target)
        result = target.as_uri()
        if parsed.query:
            result += '?' + parsed.query
        if parsed.fragment:
            result += '#' + parsed.fragment
        return f'{attr}={quote}{html.escape(result, quote=True)}{quote}'
    return re.sub(r'''\b(href|src)=(['"])(.*?)\2''', replace, document, flags=re.I)


def publish_html(project_html, document, title='외부 검수 갤러리'):
    """Publish the full gallery externally and a lightweight source-tree portal."""
    project_html = Path(project_html).resolve()
    output = media_path(project_html)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(externalize_html_links(document, project_html), encoding='utf8')
    project_html.parent.mkdir(parents=True, exist_ok=True)
    target = html.escape(output.as_uri(), quote=True)
    project_html.write_text(
        '<!doctype html><html lang="ko"><meta charset="utf-8">'
        '<meta name="viewport" content="width=device-width,initial-scale=1">'
        f'<title>{html.escape(title)}</title><style>'
        'body{max-width:760px;margin:60px auto;padding:24px;background:#111820;color:#edf1f5;'
        'font:17px/1.7 system-ui}a{color:#9bd4ff}</style>'
        f'<h1>{html.escape(title)}</h1><p>GIF 미리보기는 Unity 프로젝트 밖에 보관합니다.</p>'
        f'<p><a href="{target}">외부 갤러리 열기</a></p>'
        '<p>다른 컴퓨터에서는 로컬 미리보기를 생성한 뒤 열어 주세요. '
        '저장 위치는 OZGL3_ART_REVIEW_DIR로 변경할 수 있습니다.</p></html>', encoding='utf8')
    return output
