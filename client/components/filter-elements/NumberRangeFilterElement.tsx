'use client';

import { Form } from 'react-bootstrap';

interface Props {
    onChange(value: string): void;
    min?: number;
    max?: number;
    defaultValue: any;
}

export function NumberRangeFilterElement({ onChange, min, max, defaultValue }: Props) {
    const parseValue = (val: string): string => {
        if (!val) return '';
        return val;
    };

    return (
        <Form.Control
            type="text"
            value={parseValue(defaultValue)}
            onChange={(e) => onChange(e.target.value)}
            placeholder={`${min || 0}-${max || 100}`}
            className="bg-dark text-light border-secondary"
        />
    );
}
