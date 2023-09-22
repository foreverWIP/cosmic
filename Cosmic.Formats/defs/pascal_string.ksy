meta:
  id: pascal_string
seq:
  - id: length
    type: u1
  - id: contents
    type: str
    size: length
    encoding: ASCII