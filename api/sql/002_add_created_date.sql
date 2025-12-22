-- Add created_date column to children table if it doesn't exist
ALTER TABLE children
ADD COLUMN IF NOT EXISTS created_date timestamptz NOT NULL DEFAULT now();

-- Add created_date column to parents table if it doesn't exist
ALTER TABLE parents
ADD COLUMN IF NOT EXISTS created_date timestamptz NOT NULL DEFAULT now();
